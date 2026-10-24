using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>/api/cases/{personConditionId}/encounters</c>: the healthcare encounters
/// recorded on a case — listing, adding, saving over and removing one.
/// </summary>
/// <remarks>
/// Shaped as <see cref="VaccineEndpoints"/>' case records are. The handlers
/// are handed <see cref="EncounterQueries"/> and not the <c>DbContext</c>,
/// because the architecture test lets no <c>Survsys.Data</c> type into an
/// endpoint signature.
/// </remarks>
public static class EncounterEndpoints
{
    private const int EncounterTypeLength = 50;

    private const int DescriptionLength = 1_000;

    /// <summary>
    /// Why this is not an encounter this tier will save, or null when it is
    /// one.
    /// </summary>
    /// <remarks>
    /// Pure, and about the shape alone: whether the facility is on file is the
    /// database's to answer.
    /// </remarks>
    public static string? WhyNotAnEncounter(SaveEncounterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.EncounterDate is null)
        {
            return "encounterDate is required: an encounter is a visit on a day.";
        }

        if (request.FacilityId is { } facility && facility < 1)
        {
            return $"facilityId {facility} is not a facility. Name one GET /api/facilities "
                + "lists, or leave it out.";
        }

        if (request.EncounterType?.Trim().Length > EncounterTypeLength)
        {
            return $"encounterType is longer than {EncounterTypeLength} characters.";
        }

        if (request.Description?.Trim().Length > DescriptionLength)
        {
            return $"description is longer than {DescriptionLength} characters.";
        }

        return null;
    }

    public static IServiceCollection AddEncounters(this IServiceCollection services) =>
        services.AddScoped<EncounterQueries>();

    public static IEndpointRouteBuilder MapEncounters(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/cases/{personConditionId:int}/encounters", GetEncounters)
            .WithName("GetCaseEncounters")
            .WithSummary("A case's healthcare encounters, newest first.");

        routes.MapPost("/api/cases/{personConditionId:int}/encounters", AddEncounter)
            .WithName("AddCaseEncounter")
            .WithSummary("Records a healthcare encounter on a case.");

        routes.MapPut("/api/cases/encounters/{id:int}", UpdateEncounter)
            .WithName("UpdateCaseEncounter")
            .WithSummary("Saves over a case's healthcare encounter.");

        routes.MapDelete("/api/cases/encounters/{id:int}", DeleteEncounter)
            .WithName("DeleteCaseEncounter")
            .WithSummary("Removes a case's healthcare encounter.");

        return routes;
    }

    private static async Task<Results<Ok<IReadOnlyList<EncounterResponse>>, ProblemHttpResult>>
        GetEncounters(
            EncounterQueries encounters,
            int personConditionId,
            CancellationToken cancellationToken = default)
    {
        if (await encounters.List(personConditionId, cancellationToken) is not { } listed)
        {
            return NoSuchCase(personConditionId);
        }

        return TypedResults.Ok(listed);
    }

    private static async Task<Results<Created<EncounterResponse>, ProblemHttpResult>> AddEncounter(
        EncounterQueries encounters,
        int personConditionId,
        SaveEncounterRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotAnEncounter(request) is { } why)
        {
            return Refused(why);
        }

        if (!await encounters.CaseExists(personConditionId, cancellationToken))
        {
            return NoSuchCase(personConditionId);
        }

        if (await WhyNotItsFacility(encounters, request, cancellationToken) is { } badFacility)
        {
            return Refused(badFacility);
        }

        var created = await encounters.Add(personConditionId, request, cancellationToken);

        return TypedResults.Created($"/api/cases/encounters/{created.Id}", created);
    }

    private static async Task<Results<Ok<EncounterResponse>, ProblemHttpResult>> UpdateEncounter(
        EncounterQueries encounters,
        int id,
        SaveEncounterRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotAnEncounter(request) is { } why)
        {
            return Refused(why);
        }

        if (await WhyNotItsFacility(encounters, request, cancellationToken) is { } badFacility)
        {
            return Refused(badFacility);
        }

        if (await encounters.Update(id, request, cancellationToken) is not { } encounter)
        {
            return NoSuchEncounter(id);
        }

        return TypedResults.Ok(encounter);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteEncounter(
        EncounterQueries encounters,
        int id,
        CancellationToken cancellationToken = default) =>
        await encounters.Delete(id, cancellationToken)
            ? TypedResults.NoContent()
            : NoSuchEncounter(id);

    private static async Task<string?> WhyNotItsFacility(
        EncounterQueries encounters,
        SaveEncounterRequest request,
        CancellationToken cancellationToken) =>
        request.FacilityId is { } facilityId
            && !await encounters.FacilityExists(facilityId, cancellationToken)
            ? $"facilityId {facilityId} is not a facility on file. The facilities are the ones "
                + "GET /api/facilities lists."
            : null;

    private static ProblemHttpResult NoSuchCase(int personConditionId) =>
        TypedResults.Problem(
            detail: $"No case has id {personConditionId}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such case.");

    private static ProblemHttpResult NoSuchEncounter(int id) =>
        TypedResults.Problem(
            detail: $"No encounter has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such encounter.");

    private static ProblemHttpResult Refused(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The encounter asked for is not one this endpoint can save.");
}

/// <summary>
/// The encounter queries, and the only thing in this tier that holds the
/// <c>DbContext</c> for a case's encounters.
/// </summary>
/// <remarks>
/// Internal, as every slice's queries are. It reads the cases and the
/// facilities directly rather than through their slices' queries, as
/// <c>VaccineQueries</c> reads the cases. Adding, saving over and removing an
/// encounter runs <see cref="CaseClassifier"/> over its case, because a
/// facility encounter is what the influenza rule reads as a hospitalization.
/// </remarks>
internal sealed class EncounterQueries(SurvsysDbContext database)
{
    /// <summary>
    /// A case's encounters, newest first and then by id; null where no case
    /// has the id.
    /// </summary>
    public async Task<IReadOnlyList<EncounterResponse>?> List(
        int personConditionId,
        CancellationToken cancellationToken)
    {
        if (!await CaseExists(personConditionId, cancellationToken))
        {
            return null;
        }

        return await (
            from encounter in database.Encounters.Where(encounter =>
                encounter.PersonConditionId == personConditionId)
            join facility in database.Facilities on encounter.FacilityId equals facility.Id into atFacility
            from facility in atFacility.DefaultIfEmpty()
            orderby encounter.EncounterDate descending, encounter.Id descending
            select new EncounterResponse(
                encounter.Id,
                encounter.EncounterDate,
                encounter.EncounterType,
                encounter.FacilityId,
                facility == null ? null : facility.Name,
                encounter.Description))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Records the encounter on the case, stamping the moment, and answers it.
    /// The caller has proven the case and any facility exist.
    /// </summary>
    public async Task<EncounterResponse> Add(
        int personConditionId,
        SaveEncounterRequest request,
        CancellationToken cancellationToken)
    {
        var encounter = new Encounter
        {
            PersonConditionId = personConditionId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        Fill(encounter, request);
        database.Encounters.Add(encounter);
        await database.SaveChangesAsync(cancellationToken);
        await new CaseClassifier(database).ClassifyAsync(personConditionId, cancellationToken);

        return await Respond(encounter, cancellationToken);
    }

    /// <summary>
    /// Saves over the encounter, or answers null where no encounter has that
    /// id. The caller has proven any facility exists.
    /// </summary>
    public async Task<EncounterResponse?> Update(
        int id,
        SaveEncounterRequest request,
        CancellationToken cancellationToken)
    {
        var encounter = await database.Encounters
            .SingleOrDefaultAsync(encounter => encounter.Id == id, cancellationToken);

        if (encounter is null)
        {
            return null;
        }

        Fill(encounter, request);
        await database.SaveChangesAsync(cancellationToken);
        await new CaseClassifier(database).ClassifyAsync(encounter.PersonConditionId, cancellationToken);

        return await Respond(encounter, cancellationToken);
    }

    /// <summary>Deletes the encounter and answers whether one was there to delete.</summary>
    public async Task<bool> Delete(int id, CancellationToken cancellationToken)
    {
        var reportId = await database.Encounters
            .Where(encounter => encounter.Id == id)
            .Select(encounter => (int?)encounter.PersonConditionId)
            .SingleOrDefaultAsync(cancellationToken);

        if (reportId is not { } caseId)
        {
            return false;
        }

        await database.Encounters
            .Where(encounter => encounter.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
        await new CaseClassifier(database).ClassifyAsync(caseId, cancellationToken);

        return true;
    }

    /// <summary>Whether a case this tier knows has that id.</summary>
    public Task<bool> CaseExists(int id, CancellationToken cancellationToken) =>
        database.PersonConditions.AnyAsync(report => report.Id == id, cancellationToken);

    /// <summary>Whether a facility on file has that id.</summary>
    public Task<bool> FacilityExists(int id, CancellationToken cancellationToken) =>
        database.Facilities.AnyAsync(facility => facility.Id == id, cancellationToken);

    /// <summary>
    /// Copies the request onto the encounter. The request has passed
    /// <see cref="EncounterEndpoints.WhyNotAnEncounter"/>, so its date is set.
    /// </summary>
    private static void Fill(Encounter encounter, SaveEncounterRequest request)
    {
        encounter.EncounterDate = request.EncounterDate!.Value;
        encounter.EncounterType = Text(request.EncounterType);
        encounter.FacilityId = request.FacilityId;
        encounter.Description = Text(request.Description);
    }

    private async Task<EncounterResponse> Respond(
        Encounter encounter,
        CancellationToken cancellationToken)
    {
        var facilityName = encounter.FacilityId is { } facilityId
            ? await database.Facilities
                .Where(facility => facility.Id == facilityId)
                .Select(facility => facility.Name)
                .SingleAsync(cancellationToken)
            : null;

        return new EncounterResponse(
            encounter.Id,
            encounter.EncounterDate,
            encounter.EncounterType,
            encounter.FacilityId,
            facilityName,
            encounter.Description);
    }

    private static string? Text(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
