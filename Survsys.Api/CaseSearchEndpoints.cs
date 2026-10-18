using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// A case morbidity report with the three rows a search result names it by:
/// its patient, its condition and, where it has one, its investigating agency.
/// </summary>
/// <remarks>
/// The entity carries no navigation properties, so the search joins the four
/// tables by key and lands them here before projecting. It is a named record
/// rather than an anonymous type so that the projection to the wire —
/// <see cref="CaseSearchEndpoints.ToResponse"/> — is one public expression the
/// unit suite can compile and prove without a database, as the work queue's is.
///
/// Its members are set by initialiser and not by a constructor, and that is
/// load-bearing: EF Core follows a member it saw bound in an initialiser when
/// a later <c>Where</c> or <c>OrderBy</c> reads it back, and does not follow
/// one that went in through a constructor parameter, so a positional record
/// here translated the count and failed the ordered page.
/// </remarks>
public sealed record JoinedCase
{
    /// <summary>The report itself.</summary>
    public required PersonCondition Report { get; init; }

    /// <summary>The patient the report is about.</summary>
    public required Person Person { get; init; }

    /// <summary>The condition the report is for.</summary>
    public required Condition Condition { get; init; }

    /// <summary>The investigating agency, or null where none was chosen.</summary>
    public Agency? Agency { get; init; }
}

/// <summary>
/// The case search: <c>GET /api/cases</c>, a paged search over the case
/// morbidity reports by the criteria <see cref="CaseSearch"/> carries.
/// </summary>
/// <remarks>
/// A read under <c>/api</c>, so <see cref="PrivilegeRules"/> asks
/// <c>view_cases</c> of it without being told. The query applies each
/// criterion that is present and no other, which is what makes no criteria at
/// all the whole collection, newest record number first — a record number is
/// the year and a sequence, so descending by it is newest first, and the
/// identifier breaks the tie the same way.
///
/// The workflow state is read as the work queue reads it: a report with no
/// stored code, or a blank one, is in the initial state, both when it is
/// carried on the wire and when it is filtered for — asking for the initial
/// code finds the reports that have never been moved as well as the ones
/// stored at it, as the saved views' export already does.
/// </remarks>
public static class CaseSearchEndpoints
{
    /// <summary>
    /// A joined case as the row the search returns.
    /// </summary>
    /// <remarks>
    /// An expression and not a method so that it is translated into the
    /// query's own SELECT rather than run over every row after the fact. The
    /// patient's name is written as the People results write it, "Last,
    /// First", or the last name alone where no first name is on file; the
    /// agency's name is null exactly where the agency is.
    /// </remarks>
    public static readonly Expression<Func<JoinedCase, CaseSearchItemResponse>> ToResponse =
        joined => new CaseSearchItemResponse(
            joined.Report.Id,
            joined.Report.RecordNumber,
            joined.Person.Id,
            joined.Person.FirstName == null
                ? joined.Person.LastName
                : joined.Person.LastName + ", " + joined.Person.FirstName,
            joined.Condition.Id,
            joined.Condition.Name,
            joined.Agency == null ? null : (int?)joined.Agency.Id,
            joined.Agency == null ? null : joined.Agency.Name,
            joined.Report.WorkflowState == null || joined.Report.WorkflowState.Trim() == ""
                ? WorkflowRules.InitialCode
                : joined.Report.WorkflowState,
            joined.Report.CaseStatus,
            joined.Report.MmwrWeek,
            joined.Report.MmwrYear,
            joined.Report.EventOnsetDate);

    public static IServiceCollection AddCaseSearch(this IServiceCollection services) =>
        services.AddScoped<CaseSearchQueries>();

    public static IEndpointRouteBuilder MapCaseSearch(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/cases", GetCases)
            .WithName("GetCases")
            .WithSummary("The case morbidity reports matching the criteria given, paged and "
                + "ordered newest record number first, each with its patient, condition "
                + "and agency named.");

        return routes;
    }

    private static async Task<Results<Ok<PagedResult<CaseSearchItemResponse>>, ProblemHttpResult>> GetCases(
        CaseSearchQueries cases,
        [AsParameters] CaseSearch search,
        int page = Paging.DefaultPage,
        int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (Paging.WhyNotAPage(page, pageSize) is { } why)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The page asked for is not one this endpoint can return.");
        }

        return TypedResults.Ok(await cases.Page(search, page, pageSize, cancellationToken));
    }
}

/// <summary>
/// The one query behind the case search, against the database it is scoped to.
/// </summary>
internal sealed class CaseSearchQueries(SurvsysDbContext database)
{
    public async Task<PagedResult<CaseSearchItemResponse>> Page(
        CaseSearch search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var matching = Matching(search);

        var total = await matching.CountAsync(cancellationToken);

        var rows = await matching
            .OrderByDescending(joined => joined.Report.RecordNumber)
            .ThenByDescending(joined => joined.Report.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(CaseSearchEndpoints.ToResponse)
            .ToListAsync(cancellationToken);

        return new PagedResult<CaseSearchItemResponse>(rows, page, pageSize, total);
    }

    /// <summary>
    /// The reports matching the criteria, joined to their patient, condition
    /// and agency.
    /// </summary>
    /// <remarks>
    /// The criteria on the report are applied before the joins and the two on
    /// the patient's name after, because the name is the person's; each is
    /// applied only where it was given, so the query for no criteria is the
    /// plain join.
    /// </remarks>
    private IQueryable<JoinedCase> Matching(CaseSearch criteria)
    {
        var search = criteria.Trimmed();

        var reports = database.PersonConditions.AsQueryable();

        if (search.RecordNumber is { Length: > 0 } recordNumber)
        {
            reports = reports.Where(report => report.RecordNumber.StartsWith(recordNumber));
        }

        if (search.ConditionId is { } conditionId)
        {
            reports = reports.Where(report => report.ConditionId == conditionId);
        }

        if (search.AgencyId is { } agencyId)
        {
            reports = reports.Where(report => report.AgencyId == agencyId);
        }

        if (search.PersonId is { } personId)
        {
            reports = reports.Where(report => report.PersonId == personId);
        }

        if (search.WorkflowState is { Length: > 0 } state)
        {
            reports = state == WorkflowRules.InitialCode
                ? reports.Where(report => report.WorkflowState == null
                    || report.WorkflowState.Trim() == ""
                    || report.WorkflowState == state)
                : reports.Where(report => report.WorkflowState == state);
        }

        if (search.CaseStatus is { Length: > 0 } status)
        {
            reports = reports.Where(report => report.CaseStatus == status);
        }

        if (search.OnsetFrom is { } from)
        {
            reports = reports.Where(report => report.EventOnsetDate >= from);
        }

        if (search.OnsetTo is { } to)
        {
            reports = reports.Where(report => report.EventOnsetDate <= to);
        }

        var joined =
            from report in reports
            join person in database.Persons on report.PersonId equals person.Id
            join condition in database.Conditions on report.ConditionId equals condition.Id
            join agency in database.Agencies on report.AgencyId equals agency.Id into agencies
            from agency in agencies.DefaultIfEmpty()
            select new JoinedCase { Report = report, Person = person, Condition = condition, Agency = agency };

        if (search.LastName is { Length: > 0 } lastName)
        {
            joined = joined.Where(row => row.Person.LastName.StartsWith(lastName));
        }

        if (search.FirstName is { Length: > 0 } firstName)
        {
            joined = joined.Where(row =>
                row.Person.FirstName != null && row.Person.FirstName.StartsWith(firstName));
        }

        return joined;
    }
}
