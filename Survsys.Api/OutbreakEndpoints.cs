using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>/api/outbreaks</c>: the outbreaks a public health agency opens to group
/// the cases that belong to them, their administration, and the multi-agency
/// access an outbreak is shared across.
/// </summary>
/// <remarks>
/// The outbreaks are the <c>outbreaks</c> table and their access the
/// <c>outbreak_access</c> table, as session 51's migration created them. The
/// handlers are handed <see cref="OutbreakQueries"/> and not the
/// <c>DbContext</c>, because the architecture test lets no <c>Survsys.Data</c>
/// type into an endpoint signature, as <see cref="AgencyEndpoints"/> is handed
/// <c>AgencyQueries</c>.
///
/// The condition and the owning agency are refused where they are named and no
/// such row exists — the agency in the words <see cref="AgencyEndpoints.NotAnAgency"/>
/// uses, so a caller reads the same sentence whichever slice refused it.
/// </remarks>
public static class OutbreakEndpoints
{
    private const int NameLength = 200;

    private const int NumberLength = 50;

    /// <summary>The status of an outbreak that is still being worked.</summary>
    public const int StatusActive = 0;

    /// <summary>The status of an outbreak that is finished.</summary>
    public const int StatusCompleted = 1;

    /// <summary>
    /// The longest a section of a report may be. The column is unbounded, so
    /// this is a guard against an upload that is an accident rather than a
    /// column constraint: a hundred thousand characters is longer than any
    /// report section somebody writes and shorter than a file somebody sent by
    /// mistake.
    /// </summary>
    public const int ReportTextLength = 100_000;

    /// <summary>
    /// The projection from the stored outbreak to the list row a screen reads.
    /// Public because the unit suite proves it.
    /// </summary>
    public static readonly Expression<Func<Outbreak, OutbreakResponse>> ToResponse =
        outbreak => new OutbreakResponse(
            outbreak.Id,
            outbreak.Number,
            outbreak.Name,
            outbreak.OutbreakDate,
            outbreak.Description,
            outbreak.ConditionId,
            outbreak.AgencyId,
            outbreak.Status,
            outbreak.CreatedAt);

    /// <summary>
    /// Why this is not an outbreak this tier will save, or null when it is one.
    /// </summary>
    /// <remarks>
    /// Pure, and about the shape alone: the name is required and bounded, the
    /// number is bounded, and the status is one of the two the legacy
    /// <c>OutbreakStatus</c> enum allows. Whether the condition and the owning
    /// agency a request names exist is a question for the database, so the
    /// endpoint asks <see cref="OutbreakQueries"/> that, exactly as
    /// <see cref="AgencyEndpoints"/> leaves an agency's existence to a query —
    /// a pure validator cannot know what is on file.
    /// </remarks>
    public static string? WhyNotAnOutbreak(SaveOutbreakRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "name is required: an outbreak is chosen from a list by its name.";
        }

        if (request.Name.Trim().Length > NameLength)
        {
            return $"name is longer than {NameLength} characters.";
        }

        if (request.Number?.Trim().Length > NumberLength)
        {
            return $"number is longer than {NumberLength} characters.";
        }

        if (request.Status is not (StatusActive or StatusCompleted))
        {
            return $"status {request.Status} is not a known outbreak status. An outbreak is "
                + $"Active ({StatusActive}) or Completed ({StatusCompleted}).";
        }

        return null;
    }

    /// <summary>
    /// The sentence a condition no outbreak can be of is refused with, whether
    /// it is one nothing can have or one no condition on file has.
    /// </summary>
    public static string NotACondition(int conditionId) =>
        $"conditionId {conditionId} is not a condition this tier knows. The conditions are the "
            + "ones GET /api/conditions lists.";

    /// <summary>
    /// Registers what the outbreak endpoints need of the container.
    /// </summary>
    public static IServiceCollection AddOutbreaks(this IServiceCollection services) =>
        services.AddScoped<OutbreakQueries>();

    public static IEndpointRouteBuilder MapOutbreaks(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/outbreaks", GetOutbreaks)
            .WithName("GetOutbreaks")
            .WithSummary("The outbreaks a case can be linked to, paged and newest first.");

        routes.MapGet("/api/outbreaks/{id:int}", GetOutbreak)
            .WithName("GetOutbreak")
            .WithSummary("One outbreak, with its condition and owning agency named and its "
                + "access agencies.");

        routes.MapPost("/api/outbreaks", CreateOutbreak)
            .WithName("CreateOutbreak")
            .WithSummary("Opens an outbreak.");

        routes.MapPut("/api/outbreaks/{id:int}", UpdateOutbreak)
            .WithName("UpdateOutbreak")
            .WithSummary("Saves over an outbreak.");

        routes.MapDelete("/api/outbreaks/{id:int}", DeleteOutbreak)
            .WithName("DeleteOutbreak")
            .WithSummary("Deletes an outbreak, its access, report and activity, and unlinks its cases.");

        routes.MapGet("/api/outbreaks/{id:int}/access", GetOutbreakAccess)
            .WithName("GetOutbreakAccess")
            .WithSummary("The agencies granted sight of an outbreak, ordered by name.");

        routes.MapPut("/api/outbreaks/{id:int}/access", SetOutbreakAccess)
            .WithName("SetOutbreakAccess")
            .WithSummary("Replaces the agencies granted sight of an outbreak.");

        routes.MapGet("/api/report-sections", GetReportSections)
            .WithName("GetReportSections")
            .WithSummary("The report sections an outbreak's report is written under, in order.");

        routes.MapGet("/api/outbreaks/{id:int}/report", GetOutbreakReport)
            .WithName("GetOutbreakReport")
            .WithSummary("An outbreak's report, as the text written under each section, every "
                + "section present.");

        routes.MapPut("/api/outbreaks/{id:int}/report/{sectionId:int}", SaveOutbreakReport)
            .WithName("SaveOutbreakReport")
            .WithSummary("Writes the text for one section of an outbreak's report.");

        routes.MapGet("/api/outbreaks/{id:int}/activity", GetOutbreakActivity)
            .WithName("GetOutbreakActivity")
            .WithSummary("An outbreak's activity log, newest first.");

        routes.MapPost("/api/outbreaks/{id:int}/activity", AddOutbreakActivity)
            .WithName("AddOutbreakActivity")
            .WithSummary("Appends one entry to an outbreak's activity log.");

        routes.MapGet("/api/outbreaks/{id:int}/cases", GetOutbreakCases)
            .WithName("GetOutbreakCases")
            .WithSummary("The cases linked to an outbreak, newest first.");

        return routes;
    }

    private static async Task<Results<Ok<PagedResult<OutbreakResponse>>, ProblemHttpResult>> GetOutbreaks(
        OutbreakQueries outbreaks,
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

        return TypedResults.Ok(await outbreaks.Page(page, pageSize, cancellationToken));
    }

    private static async Task<Results<Ok<OutbreakDetailResponse>, ProblemHttpResult>> GetOutbreak(
        OutbreakQueries outbreaks,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (await outbreaks.Detail(id, cancellationToken) is not { } outbreak)
        {
            return NoSuchOutbreak(id);
        }

        return TypedResults.Ok(outbreak);
    }

    private static async Task<Results<Created<OutbreakDetailResponse>, ProblemHttpResult>> CreateOutbreak(
        OutbreakQueries outbreaks,
        SaveOutbreakRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotAnOutbreak(request) is { } why)
        {
            return Refused(why);
        }

        if (await WhyNotItsReferences(outbreaks, request, cancellationToken) is { } badReference)
        {
            return Refused(badReference);
        }

        var created = await outbreaks.Add(request, cancellationToken);

        return TypedResults.Created($"/api/outbreaks/{created.Id}", created);
    }

    private static async Task<Results<Ok<OutbreakDetailResponse>, ProblemHttpResult>> UpdateOutbreak(
        OutbreakQueries outbreaks,
        int id,
        SaveOutbreakRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotAnOutbreak(request) is { } why)
        {
            return Refused(why);
        }

        if (await WhyNotItsReferences(outbreaks, request, cancellationToken) is { } badReference)
        {
            return Refused(badReference);
        }

        if (await outbreaks.Update(id, request, cancellationToken) is not { } outbreak)
        {
            return NoSuchOutbreak(id);
        }

        return TypedResults.Ok(outbreak);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteOutbreak(
        OutbreakQueries outbreaks,
        int id,
        CancellationToken cancellationToken = default) =>
        await outbreaks.Delete(id, cancellationToken)
            ? TypedResults.NoContent()
            : NoSuchOutbreak(id);

    private static async Task<Results<Ok<IReadOnlyList<OutbreakAccessResponse>>, ProblemHttpResult>>
        GetOutbreakAccess(
            OutbreakQueries outbreaks,
            int id,
            CancellationToken cancellationToken = default)
    {
        if (await outbreaks.Access(id, cancellationToken) is not { } access)
        {
            return NoSuchOutbreak(id);
        }

        return TypedResults.Ok(access);
    }

    private static async Task<Results<Ok<IReadOnlyList<OutbreakAccessResponse>>, ProblemHttpResult>>
        SetOutbreakAccess(
            OutbreakQueries outbreaks,
            int id,
            SetOutbreakAccessRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var agencyIds = (request.AgencyIds ?? []).Distinct().ToList();

        foreach (var agencyId in agencyIds)
        {
            if (!await outbreaks.AgencyExists(agencyId, cancellationToken))
            {
                return Refused(AgencyEndpoints.NotAnAgency(agencyId));
            }
        }

        if (await outbreaks.SetAccess(id, agencyIds, cancellationToken) is not { } access)
        {
            return NoSuchOutbreak(id);
        }

        return TypedResults.Ok(access);
    }

    private static async Task<Ok<IReadOnlyList<ReportSectionResponse>>> GetReportSections(
        OutbreakQueries outbreaks,
        CancellationToken cancellationToken = default) =>
        TypedResults.Ok(await outbreaks.ReportSections(cancellationToken));

    private static async Task<Results<Ok<IReadOnlyList<OutbreakReportSectionResponse>>, ProblemHttpResult>>
        GetOutbreakReport(
            OutbreakQueries outbreaks,
            int id,
            CancellationToken cancellationToken = default)
    {
        if (await outbreaks.Report(id, cancellationToken) is not { } report)
        {
            return NoSuchOutbreak(id);
        }

        return TypedResults.Ok(report);
    }

    private static async Task<Results<Ok<IReadOnlyList<OutbreakReportSectionResponse>>, ProblemHttpResult>>
        SaveOutbreakReport(
            OutbreakQueries outbreaks,
            int id,
            int sectionId,
            SaveOutbreakReportSectionRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ReportText?.Length > ReportTextLength)
        {
            return Refused($"reportText is longer than {ReportTextLength} characters.");
        }

        if (!await outbreaks.UserExists(request.UserId, cancellationToken))
        {
            return Refused(NotAUser(request.UserId));
        }

        if (!await outbreaks.ReportSectionExists(sectionId, cancellationToken))
        {
            return NoSuchReportSection(sectionId);
        }

        if (await outbreaks.SaveReportSection(id, sectionId, request, cancellationToken) is not { } report)
        {
            return NoSuchOutbreak(id);
        }

        return TypedResults.Ok(report);
    }

    private static async Task<Results<Ok<IReadOnlyList<OutbreakUpdateResponse>>, ProblemHttpResult>>
        GetOutbreakActivity(
            OutbreakQueries outbreaks,
            int id,
            CancellationToken cancellationToken = default)
    {
        if (await outbreaks.Activity(id, cancellationToken) is not { } activity)
        {
            return NoSuchOutbreak(id);
        }

        return TypedResults.Ok(activity);
    }

    private static async Task<Results<Ok<OutbreakUpdateResponse>, ProblemHttpResult>> AddOutbreakActivity(
        OutbreakQueries outbreaks,
        int id,
        AddOutbreakUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!await outbreaks.UserExists(request.UserId, cancellationToken))
        {
            return Refused(NotAUser(request.UserId));
        }

        if (await outbreaks.AddActivity(id, request, cancellationToken) is not { } entry)
        {
            return NoSuchOutbreak(id);
        }

        return TypedResults.Ok(entry);
    }

    private static async Task<Results<Ok<IReadOnlyList<OutbreakCaseResponse>>, ProblemHttpResult>>
        GetOutbreakCases(
            OutbreakQueries outbreaks,
            int id,
            CancellationToken cancellationToken = default)
    {
        if (await outbreaks.Cases(id, cancellationToken) is not { } cases)
        {
            return NoSuchOutbreak(id);
        }

        return TypedResults.Ok(cases);
    }

    /// <summary>
    /// Why the condition or the owning agency a request names is not one this
    /// tier knows, or null when each named reference exists. An absent
    /// reference is not asked about.
    /// </summary>
    private static async Task<string?> WhyNotItsReferences(
        OutbreakQueries outbreaks,
        SaveOutbreakRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ConditionId is { } conditionId
            && !await outbreaks.ConditionExists(conditionId, cancellationToken))
        {
            return NotACondition(conditionId);
        }

        if (request.AgencyId is { } agencyId
            && !await outbreaks.AgencyExists(agencyId, cancellationToken))
        {
            return AgencyEndpoints.NotAnAgency(agencyId);
        }

        return null;
    }

    /// <summary>
    /// The sentence a user no row has is refused with, as the author of a
    /// report section or an activity entry.
    /// </summary>
    public static string NotAUser(int userId) =>
        $"userId {userId} is not a user this tier knows.";

    private static ProblemHttpResult NoSuchOutbreak(int id) =>
        TypedResults.Problem(
            detail: $"No outbreak has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such outbreak.");

    private static ProblemHttpResult NoSuchReportSection(int sectionId) =>
        TypedResults.Problem(
            detail: $"No report section has id {sectionId}. The report sections are the ones "
                + "GET /api/report-sections lists.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such report section.");

    private static ProblemHttpResult Refused(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The outbreak asked for is not one this endpoint can save.");
}

/// <summary>
/// The outbreak queries, and the only thing in this tier that holds the
/// <c>DbContext</c> for outbreaks and their access.
/// </summary>
/// <remarks>
/// Internal, as every slice's queries are. It reads the agencies and the
/// conditions directly rather than through their slices' queries: it holds the
/// one <c>DbContext</c>, and a cross-slice call would buy nothing but a second
/// hop to the same tables.
/// </remarks>
internal sealed class OutbreakQueries(SurvsysDbContext database)
{
    /// <summary>
    /// One page of outbreaks, newest first and then by id — by id as well so
    /// two outbreaks opened in the same instant keep a total order rather than
    /// letting a row appear on two pages or on neither.
    /// </summary>
    public async Task<PagedResult<OutbreakResponse>> Page(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var total = await database.Outbreaks.CountAsync(cancellationToken);

        var rows = await database.Outbreaks
            .OrderByDescending(outbreak => outbreak.CreatedAt)
            .ThenByDescending(outbreak => outbreak.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(OutbreakEndpoints.ToResponse)
            .ToListAsync(cancellationToken);

        return new PagedResult<OutbreakResponse>(rows, page, pageSize, total);
    }

    /// <summary>
    /// The outbreak with that id, its condition and owning agency named, and
    /// its access agencies; null where no outbreak has the id.
    /// </summary>
    public async Task<OutbreakDetailResponse?> Detail(int id, CancellationToken cancellationToken)
    {
        var outbreak = await database.Outbreaks
            .SingleOrDefaultAsync(outbreak => outbreak.Id == id, cancellationToken);

        if (outbreak is null)
        {
            return null;
        }

        var conditionName = outbreak.ConditionId is { } conditionId
            ? await database.Conditions
                .Where(condition => condition.Id == conditionId)
                .Select(condition => condition.Name)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        var agencyName = outbreak.AgencyId is { } agencyId
            ? await database.Agencies
                .Where(agency => agency.Id == agencyId)
                .Select(agency => agency.Name)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        var access = await AccessRows(id, cancellationToken);

        return new OutbreakDetailResponse(
            outbreak.Id,
            outbreak.Number,
            outbreak.Name,
            outbreak.OutbreakDate,
            outbreak.Description,
            outbreak.ConditionId,
            conditionName,
            outbreak.AgencyId,
            agencyName,
            outbreak.Status,
            outbreak.CreatedAt,
            access);
    }

    /// <summary>Creates the outbreak and answers its detail.</summary>
    public async Task<OutbreakDetailResponse> Add(SaveOutbreakRequest request, CancellationToken cancellationToken)
    {
        var outbreak = new Outbreak { Name = request.Name.Trim(), CreatedAt = DateTimeOffset.UtcNow };
        Apply(outbreak, request);
        database.Outbreaks.Add(outbreak);
        await database.SaveChangesAsync(cancellationToken);

        return (await Detail(outbreak.Id, cancellationToken))!;
    }

    /// <summary>
    /// Saves over the outbreak, or answers null where no outbreak has that id.
    /// The access is not touched here — it is saved through its own endpoint.
    /// </summary>
    public async Task<OutbreakDetailResponse?> Update(
        int id,
        SaveOutbreakRequest request,
        CancellationToken cancellationToken)
    {
        var outbreak = await database.Outbreaks
            .SingleOrDefaultAsync(outbreak => outbreak.Id == id, cancellationToken);

        if (outbreak is null)
        {
            return null;
        }

        Apply(outbreak, request);
        await database.SaveChangesAsync(cancellationToken);

        return await Detail(id, cancellationToken);
    }

    /// <summary>
    /// Deletes the outbreak and answers whether one was there to delete. The
    /// access, report sections and activity go with it by their cascade, and
    /// the cases that named it are left with their link nulled.
    /// </summary>
    public async Task<bool> Delete(int id, CancellationToken cancellationToken) =>
        await database.Outbreaks
            .Where(outbreak => outbreak.Id == id)
            .ExecuteDeleteAsync(cancellationToken) > 0;

    /// <summary>
    /// The agencies granted sight of the outbreak, ordered by name; null where
    /// no outbreak has the id.
    /// </summary>
    public async Task<IReadOnlyList<OutbreakAccessResponse>?> Access(
        int id,
        CancellationToken cancellationToken)
    {
        if (!await database.Outbreaks.AnyAsync(outbreak => outbreak.Id == id, cancellationToken))
        {
            return null;
        }

        return await AccessRows(id, cancellationToken);
    }

    /// <summary>
    /// Replaces the agencies granted sight of the outbreak with the ones given,
    /// and answers the new list; null where no outbreak has the id. The caller
    /// has already proven each agency exists.
    /// </summary>
    public async Task<IReadOnlyList<OutbreakAccessResponse>?> SetAccess(
        int id,
        IReadOnlyList<int> agencyIds,
        CancellationToken cancellationToken)
    {
        if (!await database.Outbreaks.AnyAsync(outbreak => outbreak.Id == id, cancellationToken))
        {
            return null;
        }

        // The difference and not a delete-all-then-insert: a grant kept on the
        // list is one row EF Core already tracks, and adding a second instance
        // under the same key would be refused.
        var stored = await database.OutbreakAccess
            .Where(access => access.OutbreakId == id)
            .ToListAsync(cancellationToken);

        database.OutbreakAccess.RemoveRange(
            stored.Where(access => !agencyIds.Contains(access.AgencyId)));
        database.OutbreakAccess.AddRange(agencyIds
            .Except(stored.Select(access => access.AgencyId))
            .Select(agencyId => new OutbreakAccess { OutbreakId = id, AgencyId = agencyId }));
        await database.SaveChangesAsync(cancellationToken);

        return await AccessRows(id, cancellationToken);
    }

    /// <summary>Whether a condition this tier knows has that id.</summary>
    public Task<bool> ConditionExists(int id, CancellationToken cancellationToken) =>
        database.Conditions.AnyAsync(condition => condition.Id == id, cancellationToken);

    /// <summary>Whether an agency this tier knows has that id.</summary>
    public Task<bool> AgencyExists(int id, CancellationToken cancellationToken) =>
        database.Agencies.AnyAsync(agency => agency.Id == id, cancellationToken);

    /// <summary>Whether a user this tier knows has that id.</summary>
    public Task<bool> UserExists(int id, CancellationToken cancellationToken) =>
        database.Users.AnyAsync(user => user.Id == id, cancellationToken);

    /// <summary>Whether a report section has that id.</summary>
    public Task<bool> ReportSectionExists(int id, CancellationToken cancellationToken) =>
        database.ReportSections.AnyAsync(section => section.Id == id, cancellationToken);

    /// <summary>The report sections, in their seeded order.</summary>
    public async Task<IReadOnlyList<ReportSectionResponse>> ReportSections(
        CancellationToken cancellationToken) =>
        await database.ReportSections
            .OrderBy(section => section.Id)
            .Select(section => new ReportSectionResponse(section.Id, section.Description))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// The outbreak's report as every section, in order, with the text written
    /// under each or null where it is unwritten; null where no outbreak has the
    /// id.
    /// </summary>
    public async Task<IReadOnlyList<OutbreakReportSectionResponse>?> Report(
        int id,
        CancellationToken cancellationToken)
    {
        if (!await database.Outbreaks.AnyAsync(outbreak => outbreak.Id == id, cancellationToken))
        {
            return null;
        }

        return await database.ReportSections
            .OrderBy(section => section.Id)
            .GroupJoin(
                database.OutbreakReportSections.Where(written => written.OutbreakId == id),
                section => section.Id,
                written => written.ReportSectionId,
                (section, written) => new { section, written })
            .SelectMany(
                pair => pair.written.DefaultIfEmpty(),
                (pair, written) => new OutbreakReportSectionResponse(
                    pair.section.Id,
                    pair.section.Description,
                    written == null ? null : written.ReportText))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Writes the text for one section of the outbreak's report, upserting the
    /// one row, and answers the whole report; null where no outbreak has the
    /// id. The caller has proven the section and the author exist.
    /// </summary>
    public async Task<IReadOnlyList<OutbreakReportSectionResponse>?> SaveReportSection(
        int id,
        int sectionId,
        SaveOutbreakReportSectionRequest request,
        CancellationToken cancellationToken)
    {
        if (!await database.Outbreaks.AnyAsync(outbreak => outbreak.Id == id, cancellationToken))
        {
            return null;
        }

        var written = await database.OutbreakReportSections.SingleOrDefaultAsync(
            section => section.OutbreakId == id && section.ReportSectionId == sectionId,
            cancellationToken);

        if (written is null)
        {
            database.OutbreakReportSections.Add(new OutbreakReportSection
            {
                OutbreakId = id,
                ReportSectionId = sectionId,
                UserId = request.UserId,
                ReportText = Text(request.ReportText),
            });
        }
        else
        {
            written.UserId = request.UserId;
            written.ReportText = Text(request.ReportText);
        }

        await database.SaveChangesAsync(cancellationToken);

        return await Report(id, cancellationToken);
    }

    /// <summary>
    /// The outbreak's activity log, newest first; null where no outbreak has
    /// the id.
    /// </summary>
    public async Task<IReadOnlyList<OutbreakUpdateResponse>?> Activity(
        int id,
        CancellationToken cancellationToken)
    {
        if (!await database.Outbreaks.AnyAsync(outbreak => outbreak.Id == id, cancellationToken))
        {
            return null;
        }

        return await database.OutbreakUpdates
            .Where(update => update.OutbreakId == id)
            .OrderByDescending(update => update.CreatedAt)
            .ThenByDescending(update => update.Id)
            .Select(update => new OutbreakUpdateResponse(
                update.Id,
                update.CreatedAt,
                update.UserId,
                update.Description,
                update.Type))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Appends one entry to the outbreak's activity log, stamping the moment,
    /// and answers it; null where no outbreak has the id. The caller has proven
    /// the author exists.
    /// </summary>
    public async Task<OutbreakUpdateResponse?> AddActivity(
        int id,
        AddOutbreakUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!await database.Outbreaks.AnyAsync(outbreak => outbreak.Id == id, cancellationToken))
        {
            return null;
        }

        var entry = new OutbreakUpdate
        {
            OutbreakId = id,
            CreatedAt = DateTimeOffset.UtcNow,
            UserId = request.UserId,
            Description = Text(request.Description),
            Type = request.Type,
        };
        database.OutbreakUpdates.Add(entry);
        await database.SaveChangesAsync(cancellationToken);

        return new OutbreakUpdateResponse(
            entry.Id,
            entry.CreatedAt,
            entry.UserId,
            entry.Description,
            entry.Type);
    }

    /// <summary>
    /// The cases linked to the outbreak, newest first, each with the person it
    /// is about and the condition; null where no outbreak has the id.
    /// </summary>
    public async Task<IReadOnlyList<OutbreakCaseResponse>?> Cases(
        int id,
        CancellationToken cancellationToken)
    {
        if (!await database.Outbreaks.AnyAsync(outbreak => outbreak.Id == id, cancellationToken))
        {
            return null;
        }

        return await (
            from report in database.PersonConditions.Where(report => report.OutbreakId == id)
            join person in database.Persons on report.PersonId equals person.Id
            join condition in database.Conditions on report.ConditionId equals condition.Id
            orderby report.CreatedAt descending, report.Id descending
            select new OutbreakCaseResponse(
                report.Id,
                report.RecordNumber,
                person.FirstName == null || person.FirstName == ""
                    ? person.LastName
                    : person.LastName + ", " + person.FirstName,
                condition.Name))
            .ToListAsync(cancellationToken);
    }

    private static string? Text(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private Task<List<OutbreakAccessResponse>> AccessRows(int id, CancellationToken cancellationToken) =>
        database.OutbreakAccess
            .Where(access => access.OutbreakId == id)
            .Join(
                database.Agencies,
                access => access.AgencyId,
                agency => agency.Id,
                (access, agency) => agency)
            .OrderBy(agency => agency.Name)
            .ThenBy(agency => agency.Id)
            .Select(agency => new OutbreakAccessResponse(agency.Id, agency.Name))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// The request's values onto the outbreak: the name and number trimmed, and
    /// a blank optional string stored as null.
    /// </summary>
    private static void Apply(Outbreak outbreak, SaveOutbreakRequest request)
    {
        outbreak.Name = request.Name.Trim();
        outbreak.Number = string.IsNullOrWhiteSpace(request.Number) ? null : request.Number.Trim();
        outbreak.OutbreakDate = request.OutbreakDate;
        outbreak.Description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        outbreak.ConditionId = request.ConditionId;
        outbreak.AgencyId = request.AgencyId;
        outbreak.Status = request.Status;
    }
}
