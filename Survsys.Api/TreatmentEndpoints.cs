using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>/api/treatments</c>: the treatment catalogue and the condition-specific
/// lists — which treatments a condition offers — that drive what an
/// investigator is shown for a case's disease.
/// </summary>
/// <remarks>
/// The treatments are the <c>treatments</c> table and their condition lists the
/// <c>condition_treatments</c> join, as this session's migration created them.
/// The handlers are handed <see cref="TreatmentQueries"/> and not the
/// <c>DbContext</c>, because the architecture test lets no <c>Survsys.Data</c>
/// type into an endpoint signature, as <see cref="OutbreakEndpoints"/> is handed
/// <c>OutbreakQueries</c>.
///
/// A condition a treatment is listed for is refused where it is named and no
/// such row exists, in the words <see cref="NotACondition"/> uses, which are
/// the same words <see cref="OutbreakEndpoints.NotACondition"/> uses, so a
/// caller reads the same sentence whichever slice refused it.
/// </remarks>
public static class TreatmentEndpoints
{
    private const int NameLength = 200;

    /// <summary>
    /// The projection from a stored treatment and the count of conditions it is
    /// listed for to the list row a screen reads.
    /// </summary>
    /// <remarks>
    /// Two parameters because the count is not the treatment's own column — it
    /// is a fact about the <c>condition_treatments</c> join — so the query
    /// works it out and hands it in, as <see cref="ClinicalEndpoints.ToResponse"/>
    /// takes the report and its person. <see cref="Respond"/> is this compiled,
    /// for the query that has the count in hand. A unit test compiles and
    /// invokes the same expression to prove what it carries; public for that
    /// reason and nothing else calls it.
    /// </remarks>
    public static readonly Expression<Func<Treatment, int, TreatmentResponse>> ToResponse =
        (treatment, conditionCount) => new TreatmentResponse(
            treatment.Id,
            treatment.Name,
            treatment.Active,
            treatment.DefaultTreatment,
            conditionCount);

    /// <summary>
    /// <see cref="ToResponse"/> compiled, for a query that has materialised the
    /// treatment and its count and so maps in memory.
    /// </summary>
    public static readonly Func<Treatment, int, TreatmentResponse> Respond = ToResponse.Compile();

    /// <summary>
    /// Why this is not a treatment this tier will save, or null when it is one.
    /// </summary>
    /// <remarks>
    /// Pure, and about the shape alone: the name is required and bounded, and no
    /// condition id appears twice in the submitted set. Whether each condition a
    /// request names exists is a question for the database, so the endpoint asks
    /// <see cref="TreatmentQueries"/> that, as <see cref="OutbreakEndpoints.WhyNotAnOutbreak"/>
    /// leaves a condition's existence to a query.
    /// </remarks>
    public static string? WhyNotATreatment(SaveTreatmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "name is required: a treatment is listed and chosen by its name.";
        }

        if (request.Name.Trim().Length > NameLength)
        {
            return $"name is longer than {NameLength} characters.";
        }

        var conditionIds = request.ConditionIds ?? [];

        if (conditionIds.Count != conditionIds.Distinct().Count())
        {
            return "conditionIds names the same condition twice: a treatment is listed for a "
                + "condition once, so each id in the set is given once.";
        }

        return null;
    }

    /// <summary>
    /// The sentence a condition no treatment can be listed for is refused with,
    /// the same one <see cref="OutbreakEndpoints.NotACondition"/> uses.
    /// </summary>
    public static string NotACondition(int conditionId) =>
        $"conditionId {conditionId} is not a condition this tier knows. The conditions are the "
            + "ones GET /api/conditions lists.";

    /// <summary>Registers what the treatment endpoints need of the container.</summary>
    public static IServiceCollection AddTreatments(this IServiceCollection services) =>
        services.AddScoped<TreatmentQueries>();

    public static IEndpointRouteBuilder MapTreatments(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/treatments", GetTreatments)
            .WithName("GetTreatments")
            .WithSummary("The treatment catalogue, paged and ordered by name.");

        routes.MapGet("/api/treatments/{id:int}", GetTreatment)
            .WithName("GetTreatment")
            .WithSummary("One treatment, with the conditions it is listed for.");

        routes.MapPost("/api/treatments", CreateTreatment)
            .WithName("CreateTreatment")
            .WithSummary("Adds a treatment and the conditions it is listed for.");

        routes.MapPut("/api/treatments/{id:int}", UpdateTreatment)
            .WithName("UpdateTreatment")
            .WithSummary("Saves over a treatment and the conditions it is listed for.");

        routes.MapGet("/api/conditions/{conditionId:int}/treatments", GetConditionTreatments)
            .WithName("GetConditionTreatments")
            .WithSummary("The treatments a condition is listed for, paged and ordered by name.");

        return routes;
    }

    private static async Task<Results<Ok<PagedResult<TreatmentResponse>>, ProblemHttpResult>> GetTreatments(
        TreatmentQueries treatments,
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

        return TypedResults.Ok(await treatments.Page(page, pageSize, cancellationToken));
    }

    private static async Task<Results<Ok<TreatmentDetailResponse>, ProblemHttpResult>> GetTreatment(
        TreatmentQueries treatments,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (await treatments.Detail(id, cancellationToken) is not { } treatment)
        {
            return NoSuchTreatment(id);
        }

        return TypedResults.Ok(treatment);
    }

    private static async Task<Results<Created<TreatmentDetailResponse>, ProblemHttpResult>> CreateTreatment(
        TreatmentQueries treatments,
        SaveTreatmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotATreatment(request) is { } why)
        {
            return Refused(why);
        }

        if (await WhyNotItsConditions(treatments, request, cancellationToken) is { } badReference)
        {
            return Refused(badReference);
        }

        var created = await treatments.Add(request, cancellationToken);

        return TypedResults.Created($"/api/treatments/{created.Id}", created);
    }

    private static async Task<Results<Ok<TreatmentDetailResponse>, ProblemHttpResult>> UpdateTreatment(
        TreatmentQueries treatments,
        int id,
        SaveTreatmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotATreatment(request) is { } why)
        {
            return Refused(why);
        }

        if (await WhyNotItsConditions(treatments, request, cancellationToken) is { } badReference)
        {
            return Refused(badReference);
        }

        if (await treatments.Update(id, request, cancellationToken) is not { } treatment)
        {
            return NoSuchTreatment(id);
        }

        return TypedResults.Ok(treatment);
    }

    private static async Task<Results<Ok<PagedResult<TreatmentResponse>>, ProblemHttpResult>>
        GetConditionTreatments(
            TreatmentQueries treatments,
            int conditionId,
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

        if (!await treatments.ConditionExists(conditionId, cancellationToken))
        {
            return NoSuchCondition(conditionId);
        }

        return TypedResults.Ok(
            await treatments.PageForCondition(conditionId, page, pageSize, cancellationToken));
    }

    /// <summary>
    /// Why a condition the request lists the treatment for is not one this tier
    /// knows, or null when each named condition exists.
    /// </summary>
    private static async Task<string?> WhyNotItsConditions(
        TreatmentQueries treatments,
        SaveTreatmentRequest request,
        CancellationToken cancellationToken)
    {
        foreach (var conditionId in (request.ConditionIds ?? []).Distinct())
        {
            if (!await treatments.ConditionExists(conditionId, cancellationToken))
            {
                return NotACondition(conditionId);
            }
        }

        return null;
    }

    private static ProblemHttpResult NoSuchTreatment(int id) =>
        TypedResults.Problem(
            detail: $"No treatment has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such treatment.");

    private static ProblemHttpResult NoSuchCondition(int conditionId) =>
        TypedResults.Problem(
            detail: $"No condition has id {conditionId}. The conditions are the ones "
                + "GET /api/conditions lists.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such condition.");

    private static ProblemHttpResult Refused(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The treatment asked for is not one this endpoint can save.");
}

/// <summary>
/// The treatment queries, and the only thing in this tier that holds the
/// <c>DbContext</c> for treatments and their condition lists.
/// </summary>
/// <remarks>
/// Internal, as every slice's queries are. It reads the conditions directly
/// rather than through their slice's queries: it holds the one
/// <c>DbContext</c>, and a cross-slice call would buy nothing but a second hop
/// to the same table.
/// </remarks>
internal sealed class TreatmentQueries(SurvsysDbContext database)
{
    /// <summary>
    /// One page of treatments, ordered by name and then by id so two treatments
    /// of the same name keep a total order.
    /// </summary>
    public async Task<PagedResult<TreatmentResponse>> Page(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var total = await database.Treatments.CountAsync(cancellationToken);

        var rows = await database.Treatments
            .OrderBy(treatment => treatment.Name)
            .ThenBy(treatment => treatment.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(treatment => new TreatmentWithCount(
                treatment,
                database.ConditionTreatments.Count(link => link.TreatmentId == treatment.Id)))
            .ToListAsync(cancellationToken);

        return new PagedResult<TreatmentResponse>(
            rows.Select(row => TreatmentEndpoints.Respond(row.Treatment, row.ConditionCount)).ToList(),
            page,
            pageSize,
            total);
    }

    /// <summary>
    /// One page of the treatments a condition is listed for, ordered the same
    /// way. The caller has proven the condition exists.
    /// </summary>
    public async Task<PagedResult<TreatmentResponse>> PageForCondition(
        int conditionId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var listed = database.ConditionTreatments
            .Where(link => link.ConditionId == conditionId)
            .Select(link => link.TreatmentId);

        var treatments = database.Treatments.Where(treatment => listed.Contains(treatment.Id));

        var total = await treatments.CountAsync(cancellationToken);

        var rows = await treatments
            .OrderBy(treatment => treatment.Name)
            .ThenBy(treatment => treatment.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(treatment => new TreatmentWithCount(
                treatment,
                database.ConditionTreatments.Count(link => link.TreatmentId == treatment.Id)))
            .ToListAsync(cancellationToken);

        return new PagedResult<TreatmentResponse>(
            rows.Select(row => TreatmentEndpoints.Respond(row.Treatment, row.ConditionCount)).ToList(),
            page,
            pageSize,
            total);
    }

    /// <summary>
    /// The treatment with that id and the conditions it is listed for, ordered
    /// by name; null where no treatment has the id.
    /// </summary>
    public async Task<TreatmentDetailResponse?> Detail(int id, CancellationToken cancellationToken)
    {
        var treatment = await database.Treatments
            .SingleOrDefaultAsync(treatment => treatment.Id == id, cancellationToken);

        if (treatment is null)
        {
            return null;
        }

        var conditions = await ConditionRows(id, cancellationToken);

        return new TreatmentDetailResponse(
            treatment.Id,
            treatment.Name,
            treatment.Active,
            treatment.DefaultTreatment,
            conditions);
    }

    /// <summary>Creates the treatment and its condition links, and answers its detail.</summary>
    public async Task<TreatmentDetailResponse> Add(
        SaveTreatmentRequest request,
        CancellationToken cancellationToken)
    {
        var treatment = new Treatment
        {
            Name = request.Name.Trim(),
            Active = request.Active,
            DefaultTreatment = request.DefaultTreatment,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        database.Treatments.Add(treatment);
        await database.SaveChangesAsync(cancellationToken);

        database.ConditionTreatments.AddRange((request.ConditionIds ?? [])
            .Distinct()
            .Select(conditionId => new ConditionTreatment
            {
                ConditionId = conditionId,
                TreatmentId = treatment.Id,
            }));
        await database.SaveChangesAsync(cancellationToken);

        return (await Detail(treatment.Id, cancellationToken))!;
    }

    /// <summary>
    /// Saves over the treatment and replaces its condition links, or answers
    /// null where no treatment has that id.
    /// </summary>
    public async Task<TreatmentDetailResponse?> Update(
        int id,
        SaveTreatmentRequest request,
        CancellationToken cancellationToken)
    {
        var treatment = await database.Treatments
            .SingleOrDefaultAsync(treatment => treatment.Id == id, cancellationToken);

        if (treatment is null)
        {
            return null;
        }

        treatment.Name = request.Name.Trim();
        treatment.Active = request.Active;
        treatment.DefaultTreatment = request.DefaultTreatment;

        var wanted = (request.ConditionIds ?? []).Distinct().ToList();

        // The difference and not a delete-all-then-insert: a link kept on the
        // set is one row EF Core already tracks, and adding a second instance
        // under the same key would be refused.
        var stored = await database.ConditionTreatments
            .Where(link => link.TreatmentId == id)
            .ToListAsync(cancellationToken);

        database.ConditionTreatments.RemoveRange(
            stored.Where(link => !wanted.Contains(link.ConditionId)));
        database.ConditionTreatments.AddRange(wanted
            .Except(stored.Select(link => link.ConditionId))
            .Select(conditionId => new ConditionTreatment
            {
                ConditionId = conditionId,
                TreatmentId = id,
            }));
        await database.SaveChangesAsync(cancellationToken);

        return await Detail(id, cancellationToken);
    }

    /// <summary>Whether a condition this tier knows has that id.</summary>
    public Task<bool> ConditionExists(int id, CancellationToken cancellationToken) =>
        database.Conditions.AnyAsync(condition => condition.Id == id, cancellationToken);

    private Task<List<TreatmentConditionResponse>> ConditionRows(
        int treatmentId,
        CancellationToken cancellationToken) =>
        database.ConditionTreatments
            .Where(link => link.TreatmentId == treatmentId)
            .Join(
                database.Conditions,
                link => link.ConditionId,
                condition => condition.Id,
                (link, condition) => new TreatmentConditionResponse(condition.Id, condition.Name))
            .OrderBy(condition => condition.Name)
            .ThenBy(condition => condition.Id)
            .ToListAsync(cancellationToken);

    /// <summary>A treatment and the count of conditions it is listed for, carried out of a query.</summary>
    private sealed record TreatmentWithCount(Treatment Treatment, int ConditionCount);
}
