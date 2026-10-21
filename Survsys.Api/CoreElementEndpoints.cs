using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>/api/core-elements</c>: core element configuration — which fields of a
/// case morbidity report are shown, by default and per condition and agency —
/// and <c>/api/person-conditions/{id}/hidden-elements</c>, what that
/// configuration hides on one report.
/// </summary>
/// <remarks>
/// The legacy Min/Opt Visibility screen's resource. The handlers are handed
/// <see cref="CoreElementQueries"/> and not the <c>DbContext</c>, because the
/// architecture test lets no <c>Survsys.Data</c> type into an endpoint
/// signature. An element, condition or agency that does not exist is a 400,
/// asked before the write rather than left to a foreign key to refuse as a
/// 500.
/// </remarks>
public static class CoreElementEndpoints
{
    /// <summary>
    /// The projection from the stored element to the wire shape. Public
    /// because the unit suite proves it.
    /// </summary>
    public static readonly Expression<Func<CoreElement, CoreElementResponse>> ToResponse =
        element => new CoreElementResponse(element.Id, element.Uid, element.Label, element.Tab, element.Hidden);

    /// <summary>
    /// <see cref="ToResponse"/> compiled, for an element just saved and so in
    /// hand rather than in a query.
    /// </summary>
    public static readonly Func<CoreElement, CoreElementResponse> Respond = ToResponse.Compile();

    /// <summary>
    /// The projection from the stored override to the wire shape. Public
    /// because the unit suite proves it.
    /// </summary>
    public static readonly Expression<Func<CoreElementOverride, CoreElementOverrideResponse>> ToOverrideResponse =
        o => new CoreElementOverrideResponse(o.Id, o.CoreElementId, o.ConditionId, o.AgencyId, o.Hidden);

    /// <summary>
    /// <see cref="ToOverrideResponse"/> compiled, for an override just saved
    /// and so in hand rather than in a query.
    /// </summary>
    public static readonly Func<CoreElementOverride, CoreElementOverrideResponse> RespondOverride =
        ToOverrideResponse.Compile();

    /// <summary>
    /// Why this is not an override this tier will save, as far as can be said
    /// without the database, or null when it is one.
    /// </summary>
    /// <remarks>
    /// A pure function the unit suite proves. Whether the element, condition
    /// and agency exist needs queries and is the endpoint's.
    /// </remarks>
    public static string? WhyNotAnOverride(SaveCoreElementOverrideRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.CoreElementId <= 0)
        {
            return "coreElementId is required: an override is for one of the elements "
                + "GET /api/core-elements lists.";
        }

        if (request.ConditionId is null && request.AgencyId is null)
        {
            return "conditionId or agencyId is required: an override for every condition "
                + "and every agency is the element's default, set with PUT /api/core-elements/{id}.";
        }

        if (request.ConditionId <= 0)
        {
            return $"conditionId {request.ConditionId} is not a condition this tier knows.";
        }

        return request.AgencyId <= 0 ? AgencyEndpoints.NotAnAgency(request.AgencyId.Value) : null;
    }

    /// <summary>
    /// Registers what the core element endpoints need of the container.
    /// </summary>
    public static IServiceCollection AddCoreElements(this IServiceCollection services) =>
        services.AddScoped<CoreElementQueries>();

    public static IEndpointRouteBuilder MapCoreElements(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/core-elements", GetCoreElements)
            .WithName("GetCoreElements")
            .WithSummary("The core elements, with their defaults.");

        routes.MapPut("/api/core-elements/{id:int}", SaveCoreElement)
            .WithName("SaveCoreElement")
            .WithSummary("Sets whether a core element is hidden by default.");

        routes.MapGet("/api/core-elements/overrides", GetOverrides)
            .WithName("GetCoreElementOverrides")
            .WithSummary("The core element overrides.");

        routes.MapPost("/api/core-elements/overrides", SaveOverride)
            .WithName("SaveCoreElementOverride")
            .WithSummary("Saves an override, replacing the one for the same combination.");

        routes.MapDelete("/api/core-elements/overrides/{id:int}", DeleteOverride)
            .WithName("DeleteCoreElementOverride")
            .WithSummary("Deletes an override.");

        routes.MapGet("/api/person-conditions/{id:int}/hidden-elements", GetHiddenElements)
            .WithName("GetHiddenElements")
            .WithSummary("The uids of the core elements hidden on a report.");

        return routes;
    }

    private static async Task<Ok<IReadOnlyList<CoreElementResponse>>> GetCoreElements(
        CoreElementQueries elements,
        CancellationToken cancellationToken = default) =>
        TypedResults.Ok(await elements.All(cancellationToken));

    private static async Task<Results<Ok<CoreElementResponse>, ProblemHttpResult>> SaveCoreElement(
        CoreElementQueries elements,
        int id,
        SaveCoreElementRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await elements.SetDefault(id, request.Hidden, cancellationToken) is not { } element)
        {
            return NotFound($"No core element has id {id}.", "There is no such core element.");
        }

        return TypedResults.Ok(element);
    }

    private static async Task<Ok<IReadOnlyList<CoreElementOverrideResponse>>> GetOverrides(
        CoreElementQueries elements,
        CancellationToken cancellationToken = default) =>
        TypedResults.Ok(await elements.Overrides(cancellationToken));

    private static async Task<Results<Ok<CoreElementOverrideResponse>, ProblemHttpResult>> SaveOverride(
        CoreElementQueries elements,
        SaveCoreElementOverrideRequest request,
        CancellationToken cancellationToken = default)
    {
        if ((WhyNotAnOverride(request) ?? await elements.WhyNotKnown(request, cancellationToken)) is { } why)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The override asked for is not one this endpoint can save.");
        }

        return TypedResults.Ok(await elements.SaveOverride(request, cancellationToken));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteOverride(
        CoreElementQueries elements,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (!await elements.DeleteOverride(id, cancellationToken))
        {
            return NotFound($"No core element override has id {id}.", "There is no such override.");
        }

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<IReadOnlyList<string>>, ProblemHttpResult>> GetHiddenElements(
        CoreElementQueries elements,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (await elements.HiddenOn(id, cancellationToken) is not { } hidden)
        {
            return NotFound($"No case morbidity report has id {id}.", "There is no such report.");
        }

        return TypedResults.Ok(hidden);
    }

    private static ProblemHttpResult NotFound(string detail, string title) =>
        TypedResults.Problem(detail: detail, statusCode: StatusCodes.Status404NotFound, title: title);
}

/// <summary>
/// The core element queries, and the only thing in this tier that holds the
/// <c>DbContext</c> for core element configuration.
/// </summary>
internal sealed class CoreElementQueries(SurvsysDbContext database)
{
    /// <summary>Every element, in the order the tabs show them.</summary>
    public async Task<IReadOnlyList<CoreElementResponse>> All(CancellationToken cancellationToken) =>
        await database.CoreElements
            .OrderBy(element => element.Id)
            .Select(CoreElementEndpoints.ToResponse)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Sets the element's default, or answers null where no element has that id.
    /// </summary>
    public async Task<CoreElementResponse?> SetDefault(int id, bool hidden, CancellationToken cancellationToken)
    {
        var element = await database.CoreElements.SingleOrDefaultAsync(element => element.Id == id, cancellationToken);

        if (element is null)
        {
            return null;
        }

        element.Hidden = hidden;
        await database.SaveChangesAsync(cancellationToken);

        return CoreElementEndpoints.Respond(element);
    }

    /// <summary>Every override, by element and then by id.</summary>
    public async Task<IReadOnlyList<CoreElementOverrideResponse>> Overrides(CancellationToken cancellationToken) =>
        await database.CoreElementOverrides
            .OrderBy(o => o.CoreElementId)
            .ThenBy(o => o.Id)
            .Select(CoreElementEndpoints.ToOverrideResponse)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Why the element, condition or agency the override names is not on
    /// file, or null where all of them are.
    /// </summary>
    public async Task<string?> WhyNotKnown(SaveCoreElementOverrideRequest request, CancellationToken cancellationToken)
    {
        if (!await database.CoreElements.AnyAsync(element => element.Id == request.CoreElementId, cancellationToken))
        {
            return $"coreElementId {request.CoreElementId} is not one of the elements GET /api/core-elements lists.";
        }

        if (request.ConditionId is { } conditionId
            && !await database.Conditions.AnyAsync(condition => condition.Id == conditionId, cancellationToken))
        {
            return $"conditionId {conditionId} is not a condition this tier knows.";
        }

        if (request.AgencyId is { } agencyId
            && !await database.Agencies.AnyAsync(agency => agency.Id == agencyId, cancellationToken))
        {
            return AgencyEndpoints.NotAnAgency(agencyId);
        }

        return null;
    }

    /// <summary>
    /// Saves the override, replacing the flag of the one already on file for
    /// the same element, condition and agency.
    /// </summary>
    public async Task<CoreElementOverrideResponse> SaveOverride(
        SaveCoreElementOverrideRequest request,
        CancellationToken cancellationToken)
    {
        var saved = await database.CoreElementOverrides.SingleOrDefaultAsync(
            o => o.CoreElementId == request.CoreElementId
                && o.ConditionId == request.ConditionId
                && o.AgencyId == request.AgencyId,
            cancellationToken);

        if (saved is null)
        {
            saved = new CoreElementOverride
            {
                CoreElementId = request.CoreElementId,
                ConditionId = request.ConditionId,
                AgencyId = request.AgencyId,
            };
            database.CoreElementOverrides.Add(saved);
        }

        saved.Hidden = request.Hidden;
        await database.SaveChangesAsync(cancellationToken);

        return CoreElementEndpoints.RespondOverride(saved);
    }

    /// <summary>Deletes the override, answering false where no override has that id.</summary>
    public async Task<bool> DeleteOverride(int id, CancellationToken cancellationToken) =>
        await database.CoreElementOverrides.Where(o => o.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    /// <summary>
    /// The uids hidden on the report for its condition and investigating
    /// agency, or null where no report has that id.
    /// </summary>
    public async Task<IReadOnlyList<string>?> HiddenOn(int reportId, CancellationToken cancellationToken)
    {
        var report = await database.PersonConditions
            .Where(report => report.Id == reportId)
            .Select(report => new { report.ConditionId, report.AgencyId })
            .SingleOrDefaultAsync(cancellationToken);

        if (report is null)
        {
            return null;
        }

        var elements = await database.CoreElements.OrderBy(element => element.Id).ToListAsync(cancellationToken);
        var overrides = await database.CoreElementOverrides.ToListAsync(cancellationToken);

        return elements
            .Where(element => CoreElementVisibilityRules.IsHidden(element, report.ConditionId, report.AgencyId, overrides))
            .Select(element => element.Uid)
            .ToList();
    }
}
