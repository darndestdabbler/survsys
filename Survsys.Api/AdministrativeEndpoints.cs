using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>GET /api/person-conditions/{id}/administrative</c>: reads a report's
/// administrative data. And
/// <c>PUT /api/person-conditions/{id}/administrative</c>: saves it.
/// </summary>
/// <remarks>
/// The administrative data is the writable fields the legacy Administrative
/// tab reads and writes to the report itself, which
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps: the two per-jurisdiction case-status keys, the outbreak-association
/// code, the two LHD investigation dates, the outcome key, the event name and
/// the two reported dates, and the outbreak this case belongs to. All are the
/// report's own, so this slice reads and writes one table, as the
/// investigation slice does. The read-only Event Information the tab shows is
/// <c>PersonConditionEndpoints</c>'s and is not repeated here; the outbreak's
/// name, which the tab shows beside its id, is read across from the
/// <c>outbreaks</c> table the one case names.
///
/// The handlers are handed <see cref="AdministrativeQueries"/> and not the
/// <c>DbContext</c>, because the architecture test lets no <c>Survsys.Data</c>
/// type into an endpoint signature at all. The save is one <c>SaveChanges</c>
/// over the one row, a whole replacement of its writable fields.
/// </remarks>
public static class AdministrativeEndpoints
{
    /// <summary>
    /// The allowed answers to the Outbreak associated dropdown —
    /// <see cref="UpdateAdministrativeRequest.OutbreakAssociated"/>. Compared
    /// case-insensitively, as the investigation slice compares its Contact
    /// Oriented codes: the legacy writes this one capitalised, as
    /// <c>../nedss/docs/db-snapshots/session-012/05-after-admin-fill.txt</c>
    /// shows with <c>Unknown</c>, and the same screen writes its sibling codes
    /// in lowercase, so neither case is the wrong one. The value is stored as
    /// sent and not folded, so what the legacy wrote reads back as the legacy
    /// wrote it.
    /// </summary>
    private static readonly string[] Answers = ["Yes", "No", "Unknown"];

    /// <summary>
    /// The projection from a report to the administrative wire shape.
    /// </summary>
    /// <remarks>
    /// One parameter, because every field it maps is the report's own,
    /// <see cref="AdministrativeResponse.OutbreakName"/> aside, which the read
    /// fills across from the outbreak the case names. A unit test compiles and
    /// invokes the expression to prove what it carries. Public because the unit
    /// suite proves it, and used compiled by both the read and the save so the
    /// two cannot drift.
    /// </remarks>
    public static readonly Expression<Func<PersonCondition, AdministrativeResponse>> ToResponse =
        report => new AdministrativeResponse(
            report.Id,
            report.LhdCaseStatusId,
            report.StateCaseStatusId,
            report.OutbreakAssociated,
            report.LhdInvestigationStartDate,
            report.LhdInvestigationCompletedDate,
            report.InvestigationOutcomeDispositionId,
            report.EventName,
            report.ResultsReportedToClinicianDate,
            report.CdcFirstVerballyNotifiedDate,
            report.OutbreakId);

    /// <summary>
    /// <see cref="ToResponse"/> compiled, for the save, which has the report in
    /// hand rather than a query to translate — so a saved report is described
    /// the same way a read one is.
    /// </summary>
    public static readonly Func<PersonCondition, AdministrativeResponse> Respond =
        ToResponse.Compile();

    /// <summary>
    /// Why the administrative data asked to be saved is not data this endpoint
    /// can save, as far as can be said without the database, or null when
    /// nothing is wrong with it.
    /// </summary>
    /// <remarks>
    /// The shape <see cref="ClinicalEndpoints.WhyNotClinical"/> has: a pure
    /// function the unit suite proves, with the endpoint turning the sentence
    /// into a 400. Two things are checked: the outbreak-association code
    /// against Yes, No or Unknown in any case, and that an investigation is
    /// not completed before it started, where both dates are given. The three
    /// keys are a legacy list's values with no list here to check them
    /// against, the event name is free, and the two reported dates are each
    /// alone, with nothing to order them against. Whether the report exists
    /// is not here, because that needs a query. Public because the unit suite
    /// proves it; nothing outside this file calls it.
    /// </remarks>
    public static string? WhyNotAdministrative(UpdateAdministrativeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.OutbreakAssociated is { } outbreak
            && !Answers.Contains(outbreak, StringComparer.OrdinalIgnoreCase))
        {
            return $"outbreakAssociated '{outbreak}' is not an answer this endpoint takes. "
                + "Whether the case is outbreak associated is one of Yes, No or Unknown in "
                + "any case, or left out where it has not been answered.";
        }

        if (request is { LhdInvestigationStartDate: { } started, LhdInvestigationCompletedDate: { } completed }
            && completed < started)
        {
            return $"lhdInvestigationCompletedDate {completed:yyyy-MM-dd} is before "
                + $"lhdInvestigationStartDate {started:yyyy-MM-dd}. An investigation is not "
                + "completed before it is started: put the dates in order, or leave one out.";
        }

        return null;
    }

    /// <summary>
    /// Registers what the administrative endpoints need of the container. Kept
    /// here with the endpoints so the slice's wiring is in one place, and so
    /// that <c>Program.cs</c> need not name an internal type.
    /// </summary>
    public static IServiceCollection AddAdministrative(this IServiceCollection services) =>
        services.AddScoped<AdministrativeQueries>();

    public static IEndpointRouteBuilder MapAdministrative(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/person-conditions/{id:int}/administrative", GetAdministrative)
            .WithName("GetAdministrative")
            .WithSummary("A case morbidity report's administrative data.");

        routes.MapPut("/api/person-conditions/{id:int}/administrative", PutAdministrative)
            .WithName("PutAdministrative")
            .WithSummary("Saves a case morbidity report's administrative data.");

        return routes;
    }

    private static async Task<Results<Ok<AdministrativeResponse>, ProblemHttpResult>> GetAdministrative(
        AdministrativeQueries administrative,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (await administrative.Find(id, cancellationToken) is not { } response)
        {
            return NoSuchReport(id);
        }

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<AdministrativeResponse>, ProblemHttpResult>> PutAdministrative(
        AdministrativeQueries administrative,
        int id,
        UpdateAdministrativeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotAdministrative(request) is { } why)
        {
            return Refused(why);
        }

        if (request.OutbreakId is { } outbreakId
            && !await administrative.OutbreakExists(outbreakId, cancellationToken))
        {
            return Refused($"outbreakId {outbreakId} is not an outbreak this tier knows. The "
                + "outbreaks are the ones GET /api/outbreaks lists.");
        }

        if (await administrative.Save(id, request, cancellationToken) is not { } response)
        {
            return NoSuchReport(id);
        }

        return TypedResults.Ok(response);
    }

    private static ProblemHttpResult Refused(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The administrative data asked to be saved is not data this endpoint can save.");

    private static ProblemHttpResult NoSuchReport(int id) =>
        TypedResults.Problem(
            detail: $"No case morbidity report has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such case morbidity report.");
}

/// <summary>
/// The administrative data query and the save, and the only thing in this
/// tier that holds the <c>DbContext</c> for them.
/// </summary>
/// <remarks>
/// Internal, and in this file rather than its own, for the reason
/// <c>ClinicalQueries</c> is: it exists only to serve
/// <see cref="AdministrativeEndpoints"/>, and it is the named step between an
/// endpoint signature and <c>Survsys.Data</c> that the architecture test
/// requires.
/// </remarks>
internal sealed class AdministrativeQueries(SurvsysDbContext database)
{
    /// <summary>
    /// One report's administrative data, or null where no report has that id
    /// — the answer the endpoint turns into a 404.
    /// </summary>
    public async Task<AdministrativeResponse?> Find(int id, CancellationToken cancellationToken)
    {
        var row = await database.PersonConditions
            .Where(report => report.Id == id)
            .Select(report => new
            {
                Report = report,
                OutbreakName = database.Outbreaks
                    .Where(outbreak => outbreak.Id == report.OutbreakId)
                    .Select(outbreak => outbreak.Name)
                    .FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : AdministrativeEndpoints.Respond(row.Report) with { OutbreakName = row.OutbreakName };
    }

    /// <summary>Whether an outbreak this tier knows has that id.</summary>
    public Task<bool> OutbreakExists(int id, CancellationToken cancellationToken) =>
        database.Outbreaks.AnyAsync(outbreak => outbreak.Id == id, cancellationToken);

    /// <summary>
    /// Saves the administrative data onto the report and answers with it as
    /// stored, or null where no report has that id.
    /// </summary>
    /// <remarks>
    /// All ten fields are set and committed in one
    /// <see cref="SurvsysDbContext.SaveChangesAsync(CancellationToken)"/>: the
    /// save is a whole replacement, so a field sent null clears what was
    /// there, as the legacy tab does when a control is blanked.
    /// </remarks>
    public async Task<AdministrativeResponse?> Save(
        int id,
        UpdateAdministrativeRequest request,
        CancellationToken cancellationToken)
    {
        var report = await database.PersonConditions
            .SingleOrDefaultAsync(report => report.Id == id, cancellationToken);

        if (report is null)
        {
            return null;
        }

        report.LhdCaseStatusId = request.LhdCaseStatusId;
        report.StateCaseStatusId = request.StateCaseStatusId;
        report.OutbreakAssociated = request.OutbreakAssociated;
        report.LhdInvestigationStartDate = request.LhdInvestigationStartDate;
        report.LhdInvestigationCompletedDate = request.LhdInvestigationCompletedDate;
        report.InvestigationOutcomeDispositionId = request.InvestigationOutcomeDispositionId;
        report.EventName = request.EventName;
        report.ResultsReportedToClinicianDate = request.ResultsReportedToClinicianDate;
        report.CdcFirstVerballyNotifiedDate = request.CdcFirstVerballyNotifiedDate;
        report.OutbreakId = request.OutbreakId;

        await new CaseNotificationQueue(database).QueueChangeAsync(
            new CaseChanged(report.Id, DateTime.UtcNow), cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        await new CaseClassifier(database).ClassifyAsync(report.Id, cancellationToken);

        var outbreakName = report.OutbreakId is { } outbreakId
            ? await database.Outbreaks
                .Where(outbreak => outbreak.Id == outbreakId)
                .Select(outbreak => outbreak.Name)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        return AdministrativeEndpoints.Respond(report) with { OutbreakName = outbreakName };
    }
}
