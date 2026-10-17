using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>GET /api/person-conditions/{id}/investigation</c>: reads a report's
/// investigation data. And <c>PUT /api/person-conditions/{id}/investigation</c>:
/// saves it.
/// </summary>
/// <remarks>
/// The investigation data is the fields the legacy Investigation tab reads and
/// writes to the report itself, which
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps: the four Contact Oriented codes, the imported-from key, the
/// transmission-mode code and the four free-text fields. All ten are the
/// report's own, so this slice reads and writes one table, where the clinical
/// slice joins the report to its person.
///
/// The handlers are handed <see cref="InvestigationQueries"/> and not the
/// <c>DbContext</c>, because the architecture test lets no <c>Survsys.Data</c>
/// type into an endpoint signature at all. The save is one <c>SaveChanges</c>
/// over the one row, a whole replacement of the ten fields.
/// </remarks>
public static class InvestigationEndpoints
{
    /// <summary>
    /// The allowed answers to a Contact Oriented dropdown —
    /// <see cref="UpdateInvestigationRequest.FoodHandler"/> and its three
    /// siblings. Compared case-insensitively, because the legacy screen shows
    /// "Unknown" and writes <c>unknown</c>, which
    /// <c>../nedss/docs/db-snapshots/session-012/03-after-investigation-fill.txt</c>
    /// shows, and a code that round-trips through that screen must be accepted
    /// in either case. The value is stored as sent and not folded, so what the
    /// legacy wrote reads back as the legacy wrote it.
    /// </summary>
    private static readonly string[] Answers = ["Yes", "No", "Unknown"];

    /// <summary>
    /// The projection from a report to the investigation wire shape.
    /// </summary>
    /// <remarks>
    /// One parameter, because all ten fields are the report's own: EF Core
    /// translates it as the <c>SELECT</c> of the read. A unit test compiles and
    /// invokes the same expression to prove what it carries. Public because
    /// the unit suite proves it; nothing else names it.
    /// </remarks>
    public static readonly Expression<Func<PersonCondition, InvestigationResponse>> ToResponse =
        report => new InvestigationResponse(
            report.Id,
            report.FoodHandler,
            report.HealthcareWorker,
            report.GroupLiving,
            report.DayCareAssociation,
            report.ImportedFromId,
            report.TransmissionMode,
            report.RiskFactors,
            report.RiskFactorsNotes,
            report.OtherData1,
            report.OtherData2);

    /// <summary>
    /// <see cref="ToResponse"/> compiled, for the save, which has the report in
    /// hand rather than a query to translate — so a saved report is described
    /// the same way a read one is.
    /// </summary>
    public static readonly Func<PersonCondition, InvestigationResponse> Respond =
        ToResponse.Compile();

    /// <summary>
    /// Why the investigation data asked to be saved is not data this endpoint
    /// can save, as far as can be said without the database, or null when
    /// nothing is wrong with it.
    /// </summary>
    /// <remarks>
    /// The shape <see cref="ClinicalEndpoints.WhyNotClinical"/> has: a pure
    /// function the unit suite proves, with the endpoint turning the sentence
    /// into a 400. Only the four Contact Oriented codes are checked, each
    /// against Yes, No or Unknown in any case; the imported-from key and the
    /// transmission mode are a legacy list's values with no list here to check
    /// them against, and the free text is free. Whether the report exists is
    /// not here, because that needs a query. Public because the unit suite
    /// proves it; nothing outside this file calls it.
    /// </remarks>
    public static string? WhyNotInvestigation(UpdateInvestigationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return WhyNotAnAnswer("foodHandler", request.FoodHandler)
            ?? WhyNotAnAnswer("healthcareWorker", request.HealthcareWorker)
            ?? WhyNotAnAnswer("groupLiving", request.GroupLiving)
            ?? WhyNotAnAnswer("dayCareAssociation", request.DayCareAssociation);
    }

    /// <summary>
    /// Why one Contact Oriented code is not an answer, naming the field it was
    /// sent as, or null where it is one or was left out.
    /// </summary>
    private static string? WhyNotAnAnswer(string field, string? code)
    {
        if (code is null || Answers.Contains(code, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        return $"{field} '{code}' is not an answer this endpoint takes. A Contact Oriented "
            + "answer is one of Yes, No or Unknown in any case, or left out where it has not "
            + "been answered.";
    }

    /// <summary>
    /// Registers what the investigation endpoints need of the container. Kept
    /// here with the endpoints so the slice's wiring is in one place, and so
    /// that <c>Program.cs</c> need not name an internal type.
    /// </summary>
    public static IServiceCollection AddInvestigation(this IServiceCollection services) =>
        services.AddScoped<InvestigationQueries>();

    public static IEndpointRouteBuilder MapInvestigation(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/person-conditions/{id:int}/investigation", GetInvestigation)
            .WithName("GetInvestigation")
            .WithSummary("A case morbidity report's investigation data.");

        routes.MapPut("/api/person-conditions/{id:int}/investigation", PutInvestigation)
            .WithName("PutInvestigation")
            .WithSummary("Saves a case morbidity report's investigation data.");

        return routes;
    }

    private static async Task<Results<Ok<InvestigationResponse>, ProblemHttpResult>> GetInvestigation(
        InvestigationQueries investigation,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (await investigation.Find(id, cancellationToken) is not { } response)
        {
            return NoSuchReport(id);
        }

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<InvestigationResponse>, ProblemHttpResult>> PutInvestigation(
        InvestigationQueries investigation,
        int id,
        UpdateInvestigationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotInvestigation(request) is { } why)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The investigation data asked to be saved is not data this endpoint can save.");
        }

        if (await investigation.Save(id, request, cancellationToken) is not { } response)
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
/// The investigation data query and the save, and the only thing in this tier
/// that holds the <c>DbContext</c> for them.
/// </summary>
/// <remarks>
/// Internal, and in this file rather than its own, for the reason
/// <c>ClinicalQueries</c> is: it exists only to serve
/// <see cref="InvestigationEndpoints"/>, and it is the named step between an
/// endpoint signature and <c>Survsys.Data</c> that the architecture test
/// requires.
/// </remarks>
internal sealed class InvestigationQueries(SurvsysDbContext database)
{
    /// <summary>
    /// One report's investigation data, or null where no report has that id —
    /// the answer the endpoint turns into a 404.
    /// </summary>
    public Task<InvestigationResponse?> Find(int id, CancellationToken cancellationToken) =>
        database.PersonConditions
            .Where(report => report.Id == id)
            .Select(InvestigationEndpoints.ToResponse)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Saves the investigation data onto the report and answers with it as
    /// stored, or null where no report has that id.
    /// </summary>
    /// <remarks>
    /// All ten fields are set and committed in one
    /// <see cref="SurvsysDbContext.SaveChangesAsync(CancellationToken)"/>: the
    /// save is a whole replacement, so a field sent null clears what was
    /// there, as the legacy tab does when a control is blanked.
    /// </remarks>
    public async Task<InvestigationResponse?> Save(
        int id,
        UpdateInvestigationRequest request,
        CancellationToken cancellationToken)
    {
        var report = await database.PersonConditions
            .SingleOrDefaultAsync(report => report.Id == id, cancellationToken);

        if (report is null)
        {
            return null;
        }

        report.FoodHandler = request.FoodHandler;
        report.HealthcareWorker = request.HealthcareWorker;
        report.GroupLiving = request.GroupLiving;
        report.DayCareAssociation = request.DayCareAssociation;
        report.ImportedFromId = request.ImportedFromId;
        report.TransmissionMode = request.TransmissionMode;
        report.RiskFactors = request.RiskFactors;
        report.RiskFactorsNotes = request.RiskFactorsNotes;
        report.OtherData1 = request.OtherData1;
        report.OtherData2 = request.OtherData2;

        await database.SaveChangesAsync(cancellationToken);

        return InvestigationEndpoints.Respond(report);
    }
}
