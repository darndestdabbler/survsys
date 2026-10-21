using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>GET /api/person-conditions/{id}/clinical</c>: reads a report's clinical
/// data. And <c>PUT /api/person-conditions/{id}/clinical</c>: saves it.
/// </summary>
/// <remarks>
/// The clinical data is the fields the legacy Clinical tab reads and writes,
/// which <c>../nedss/docs/functional/epitrax-cmr-deep-editing.md</c> section 3
/// maps. Three of them are the report's own — the disease onset date, the date
/// diagnosed and whether treatment was given — and two are its
/// <em>person's</em> — the died? answer and the date of death — so this slice
/// reads and writes across both tables as the Clinical tab does, and gathers
/// them into one resource so the tab reads and saves one shape.
///
/// The handlers are handed <see cref="ClinicalQueries"/> and not the
/// <c>DbContext</c>, because the architecture test lets no <c>Survsys.Data</c>
/// type into an endpoint signature at all. The save updates the report and its
/// person in one <c>SaveChanges</c>, so a save either lands whole or not at
/// all.
/// </remarks>
public static class ClinicalEndpoints
{
    /// <summary>
    /// The allowed answers to a Yes/No/Unknown clinical control —
    /// <see cref="UpdateClinicalRequest.DeathStatus"/> and
    /// <see cref="UpdateClinicalRequest.TreatmentGiven"/>. Ordinal, because
    /// these are legacy codes and not display text.
    /// </summary>
    private static readonly string[] Answers = ["Yes", "No", "Unknown"];

    /// <summary>The one answer a date of death goes with.</summary>
    private const string Died = "Yes";

    /// <summary>The one answer a due date and a delivery facility go with.</summary>
    private const string IsPregnant = "Yes";

    /// <summary>
    /// The projection from a report and its person to the clinical wire shape.
    /// </summary>
    /// <remarks>
    /// Two parameters because the clinical data is two tables' — which is
    /// exactly a join's result selector, so EF Core translates it as the
    /// <c>SELECT</c> of the join the read makes. A unit test compiles and
    /// invokes the same expression to prove what it carries. Public because the
    /// unit suite proves it; nothing else names it.
    /// </remarks>
    public static readonly Expression<Func<PersonCondition, Person, ClinicalResponse>> ToResponse =
        (report, person) => new ClinicalResponse(
            report.Id,
            report.DiseaseOnsetDate,
            report.DateDiagnosed,
            report.TreatmentGiven,
            person.DeathStatus,
            person.DateOfDeath,
            report.Pregnant,
            report.PregnancyDueDate,
            report.ExpectedDeliveryFacilityId);

    /// <summary>
    /// <see cref="ToResponse"/> compiled, for the save, which has the report
    /// and the person in hand rather than a query to translate — so a saved
    /// report is described the same way a read one is.
    /// </summary>
    public static readonly Func<PersonCondition, Person, ClinicalResponse> Respond =
        ToResponse.Compile();

    /// <summary>
    /// Why the clinical data asked to be saved is not data this endpoint can
    /// save, as far as can be said without the database, or null when nothing
    /// is wrong with it.
    /// </summary>
    /// <remarks>
    /// The shape <see cref="PersonConditionEndpoints.WhyNotAReport"/> has: a
    /// pure function the unit suite proves, with the endpoint turning the
    /// sentence into a 400. Whether the report exists is not here, because that
    /// needs a query. Public because the unit suite proves it; nothing outside
    /// this file calls it.
    /// </remarks>
    public static string? WhyNotClinical(UpdateClinicalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.DeathStatus is { } death && !Answers.Contains(death, StringComparer.Ordinal))
        {
            return $"deathStatus '{death}' is not an answer this endpoint takes. The died? "
                + "answer is one of Yes, No or Unknown, or left out where it has not been "
                + "answered.";
        }

        if (request.TreatmentGiven is { } treatment
            && !Answers.Contains(treatment, StringComparer.Ordinal))
        {
            return $"treatmentGiven '{treatment}' is not an answer this endpoint takes. "
                + "Whether treatment was given is one of Yes, No or Unknown, or left out where "
                + "it has not been answered.";
        }

        if (request.DateOfDeath is not null && request.DeathStatus != Died)
        {
            return "dateOfDeath is set on a report whose died? answer is not Yes. A date of "
                + "death belongs to a person who died: set deathStatus to Yes, or leave "
                + "dateOfDeath out.";
        }

        if (request.Pregnant is { } pregnant && !Answers.Contains(pregnant, StringComparer.Ordinal))
        {
            return $"pregnant '{pregnant}' is not an answer this endpoint takes. Whether the "
                + "person is pregnant is one of Yes, No or Unknown, or left out where it has not "
                + "been answered.";
        }

        if ((request.PregnancyDueDate is not null || request.ExpectedDeliveryFacilityId is not null)
            && request.Pregnant != IsPregnant)
        {
            return "pregnancyDueDate or expectedDeliveryFacilityId is set on a report whose "
                + "pregnant answer is not Yes. A due date and a delivery facility belong to a "
                + "pregnancy: set pregnant to Yes, or leave them out.";
        }

        return null;
    }

    /// <summary>
    /// The MMWR week and year a report's clinical dates place it in, or null
    /// where there is no date to read one off.
    /// </summary>
    /// <remarks>
    /// The disease onset date is the one a report is reported under where it is
    /// known, and the date diagnosed stands in for it where it is not, as the
    /// legacy reads the reporting week off the onset and falls back to the
    /// diagnosis. Where neither is known there is nothing to recompute, so the
    /// caller leaves the stored MMWR as it is rather than clearing it. The week
    /// itself is <see cref="MmwrWeek.Of(DateOnly)"/>'s and no other rule's —
    /// this only chooses the date. Public because the unit suite proves it.
    /// </remarks>
    public static MmwrWeek? MmwrFor(DateOnly? diseaseOnsetDate, DateOnly? dateDiagnosed) =>
        (diseaseOnsetDate ?? dateDiagnosed) is { } date ? MmwrWeek.Of(date) : null;

    /// <summary>
    /// Registers what the clinical endpoints need of the container. Kept here
    /// with the endpoints so the slice's wiring is in one place, and so that
    /// <c>Program.cs</c> need not name an internal type.
    /// </summary>
    public static IServiceCollection AddClinical(this IServiceCollection services) =>
        services.AddScoped<ClinicalQueries>();

    public static IEndpointRouteBuilder MapClinical(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/person-conditions/{id:int}/clinical", GetClinical)
            .WithName("GetClinical")
            .WithSummary("A case morbidity report's clinical data.");

        routes.MapPut("/api/person-conditions/{id:int}/clinical", PutClinical)
            .WithName("PutClinical")
            .WithSummary("Saves a case morbidity report's clinical data.");

        return routes;
    }

    private static async Task<Results<Ok<ClinicalResponse>, ProblemHttpResult>> GetClinical(
        ClinicalQueries clinical,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (await clinical.Find(id, cancellationToken) is not { } response)
        {
            return NoSuchReport(id);
        }

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<ClinicalResponse>, ProblemHttpResult>> PutClinical(
        ClinicalQueries clinical,
        int id,
        UpdateClinicalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotClinical(request) is { } why)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The clinical data asked to be saved is not data this endpoint can save.");
        }

        if (request.ExpectedDeliveryFacilityId is { } facilityId
            && !await clinical.FacilityExists(facilityId, cancellationToken))
        {
            return TypedResults.Problem(
                detail: $"expectedDeliveryFacilityId {facilityId} is not a facility on file. The "
                    + "facilities are the ones GET /api/facilities lists.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "The clinical data asked to be saved is not data this endpoint can save.");
        }

        if (await clinical.Save(id, request, cancellationToken) is not { } response)
        {
            return NoSuchReport(id);
        }

        return TypedResults.Ok(response);
    }

    private static ProblemHttpResult NoSuchReport(int id) =>
        TypedResults.Problem(
            detail: $"No case morbidity report has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such case morbidity report.");
}

/// <summary>
/// The clinical data queries and the save, and the only thing in this tier
/// that holds the <c>DbContext</c> for them.
/// </summary>
/// <remarks>
/// Internal, and in this file rather than its own, for the reason
/// <c>PersonConditionQueries</c> is: it exists only to serve
/// <see cref="ClinicalEndpoints"/>, and it is the named step between an
/// endpoint signature and <c>Survsys.Data</c> that the architecture test
/// requires.
/// </remarks>
internal sealed class ClinicalQueries(SurvsysDbContext database)
{
    /// <summary>
    /// One report's clinical data, read across the report and its person, or
    /// null where no report has that id — the answer the endpoint turns into a
    /// 404.
    /// </summary>
    public Task<ClinicalResponse?> Find(int id, CancellationToken cancellationToken) =>
        database.PersonConditions
            .Where(report => report.Id == id)
            .Join(
                database.Persons,
                report => report.PersonId,
                person => person.Id,
                ClinicalEndpoints.ToResponse)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Saves the clinical data onto the report and its person and answers with
    /// it as stored, or null where no report has that id.
    /// </summary>
    /// <remarks>
    /// The report's own fields and the person's are set and committed in one
    /// <see cref="SurvsysDbContext.SaveChangesAsync(CancellationToken)"/>, so
    /// the save of the two tables the Clinical tab writes lands whole or not at
    /// all. The person is reached by the report's own
    /// <see cref="PersonCondition.PersonId"/> rather than by a navigation,
    /// which this schema does not have; a report's person always exists,
    /// because the report's person foreign key requires it.
    ///
    /// The MMWR week and year are read off the clinical dates here, by
    /// <see cref="ClinicalEndpoints.MmwrFor(DateOnly?, DateOnly?)"/>: a save
    /// that changes the disease onset date, or the date diagnosed where there
    /// is no onset, moves the report to the MMWR week that date falls in, and
    /// a save that leaves both unknown leaves the stored MMWR untouched. The
    /// create reads the MMWR off the event onset date instead; here it follows
    /// the dates the Clinical tab writes, which is where a correction to them
    /// arrives.
    /// </remarks>
    public async Task<ClinicalResponse?> Save(
        int id,
        UpdateClinicalRequest request,
        CancellationToken cancellationToken)
    {
        var report = await database.PersonConditions
            .SingleOrDefaultAsync(report => report.Id == id, cancellationToken);

        if (report is null)
        {
            return null;
        }

        var person = await database.Persons
            .SingleAsync(person => person.Id == report.PersonId, cancellationToken);

        report.DiseaseOnsetDate = request.DiseaseOnsetDate;
        report.DateDiagnosed = request.DateDiagnosed;
        report.TreatmentGiven = request.TreatmentGiven;
        person.DeathStatus = request.DeathStatus;
        person.DateOfDeath = request.DateOfDeath;
        report.Pregnant = request.Pregnant;
        report.PregnancyDueDate = request.PregnancyDueDate;
        report.ExpectedDeliveryFacilityId = request.ExpectedDeliveryFacilityId;

        // The MMWR week and year are read off the dates just written, so a
        // correction to the onset or the diagnosis date moves the report to
        // the week it now belongs to; where neither date is given there is
        // nothing to read one off, and the stored MMWR is left as it is.
        if (ClinicalEndpoints.MmwrFor(report.DiseaseOnsetDate, report.DateDiagnosed) is { } mmwr)
        {
            report.MmwrWeek = mmwr.Week;
            report.MmwrYear = mmwr.Year;
        }

        await new CaseNotificationQueue(database).QueueChangeAsync(
            new CaseChanged(report.Id, DateTime.UtcNow), cancellationToken);
        await database.SaveChangesAsync(cancellationToken);

        return ClinicalEndpoints.Respond(report, person);
    }

    /// <summary>Whether a facility on file has that id.</summary>
    public Task<bool> FacilityExists(int id, CancellationToken cancellationToken) =>
        database.Facilities.AnyAsync(facility => facility.Id == id, cancellationToken);
}
