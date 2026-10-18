using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>/api/vaccines</c>: the vaccine catalogue, and the vaccine records a case
/// carries — the vaccines administered to the case's person.
/// </summary>
/// <remarks>
/// The catalogue is the seeded <c>vaccines</c> table and the records the
/// <c>person_vaccines</c> table, as this session's migration created them. The
/// handlers are handed <see cref="VaccineQueries"/> and not the
/// <c>DbContext</c>, because the architecture test lets no <c>Survsys.Data</c>
/// type into an endpoint signature, as <see cref="OutbreakEndpoints"/> is handed
/// <c>OutbreakQueries</c>.
///
/// The catalogue is read-only here: a vaccine is seeded reference data an
/// operator chooses from, not one they maintain. The records are a case's, so
/// they are reached under the case the way the other case-scoped slices are.
/// </remarks>
public static class VaccineEndpoints
{
    private const int LotNumberLength = 100;

    /// <summary>
    /// The longest a record's comment may be. The column is unbounded, so this
    /// is a guard against an upload that is an accident rather than a column
    /// constraint, as <see cref="OutbreakEndpoints.ReportTextLength"/> guards a
    /// report section.
    /// </summary>
    public const int CommentLength = 1_000;

    /// <summary>
    /// The projection from a stored vaccine to the catalogue row a picker reads.
    /// Public because the unit suite proves it; nothing else calls it.
    /// </summary>
    public static readonly Expression<Func<Vaccine, VaccineResponse>> ToResponse =
        vaccine => new VaccineResponse(vaccine.Id, vaccine.Code, vaccine.Description);

    /// <summary>
    /// The projection from a stored record and its vaccine's description to the
    /// record row a case's tab reads.
    /// </summary>
    /// <remarks>
    /// Two parameters because the vaccine's description is not the record's own
    /// column — it is the catalogue's — so the query works it out and hands it
    /// in, as <see cref="TreatmentEndpoints.ToResponse"/> takes the count.
    /// <see cref="RespondRecord"/> is this compiled, for the query that has the
    /// description in hand. A unit test compiles and invokes the same
    /// expression to prove what it carries.
    /// </remarks>
    public static readonly Expression<Func<PersonVaccine, string?, VaccineRecordResponse>> RecordToResponse =
        (record, vaccineDescription) => new VaccineRecordResponse(
            record.Id,
            record.VaccineId,
            vaccineDescription,
            record.AdministeredDate,
            record.DoseNumber,
            record.LotNumber,
            record.ExpirationDate,
            record.Comment);

    /// <summary>
    /// <see cref="RecordToResponse"/> compiled, for a query that has
    /// materialised the record and its vaccine's description and so maps in
    /// memory.
    /// </summary>
    public static readonly Func<PersonVaccine, string?, VaccineRecordResponse> RespondRecord =
        RecordToResponse.Compile();

    /// <summary>
    /// Why this is not a vaccine record this tier will save, or null when it is
    /// one.
    /// </summary>
    /// <remarks>
    /// Pure, and about the shape alone: a vaccine is chosen, the dose number is
    /// one or more, the expiration date is not before the administered date, and
    /// the lot number and comment are within their bounds. Whether the vaccine
    /// and the case exist is a question for the database, which the endpoint asks
    /// <see cref="VaccineQueries"/>.
    /// </remarks>
    public static string? WhyNotAVaccineRecord(SaveVaccineRecordRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.VaccineId < 1)
        {
            return "vaccineId is required: a record names the vaccine administered, chosen from "
                + "the ones GET /api/vaccines lists.";
        }

        if (request.DoseNumber is { } dose && dose < 1)
        {
            return $"doseNumber {dose} is not a dose. A dose number is one or more, or left out "
                + "where it is not recorded.";
        }

        if (request.AdministeredDate is { } administered
            && request.ExpirationDate is { } expiration
            && expiration < administered)
        {
            return "expirationDate is before administeredDate: a lot does not expire before it "
                + "is given.";
        }

        if (request.LotNumber?.Trim().Length > LotNumberLength)
        {
            return $"lotNumber is longer than {LotNumberLength} characters.";
        }

        if (request.Comment?.Length > CommentLength)
        {
            return $"comment is longer than {CommentLength} characters.";
        }

        return null;
    }

    /// <summary>Registers what the vaccine endpoints need of the container.</summary>
    public static IServiceCollection AddVaccines(this IServiceCollection services) =>
        services.AddScoped<VaccineQueries>();

    public static IEndpointRouteBuilder MapVaccines(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/vaccines", GetVaccines)
            .WithName("GetVaccines")
            .WithSummary("The vaccine catalogue, in sort order, for the record picker.");

        routes.MapGet("/api/cases/{personConditionId:int}/vaccines", GetCaseVaccines)
            .WithName("GetCaseVaccines")
            .WithSummary("A case's vaccine records, newest administered first.");

        routes.MapPost("/api/cases/{personConditionId:int}/vaccines", AddCaseVaccine)
            .WithName("AddCaseVaccine")
            .WithSummary("Records a vaccine administered to a case.");

        routes.MapPut("/api/cases/vaccines/{id:int}", UpdateCaseVaccine)
            .WithName("UpdateCaseVaccine")
            .WithSummary("Saves over a case's vaccine record.");

        routes.MapDelete("/api/cases/vaccines/{id:int}", DeleteCaseVaccine)
            .WithName("DeleteCaseVaccine")
            .WithSummary("Removes a case's vaccine record.");

        return routes;
    }

    private static async Task<Ok<IReadOnlyList<VaccineResponse>>> GetVaccines(
        VaccineQueries vaccines,
        CancellationToken cancellationToken = default) =>
        TypedResults.Ok(await vaccines.Catalogue(cancellationToken));

    private static async Task<Results<Ok<IReadOnlyList<VaccineRecordResponse>>, ProblemHttpResult>>
        GetCaseVaccines(
            VaccineQueries vaccines,
            int personConditionId,
            CancellationToken cancellationToken = default)
    {
        if (await vaccines.Records(personConditionId, cancellationToken) is not { } records)
        {
            return NoSuchCase(personConditionId);
        }

        return TypedResults.Ok(records);
    }

    private static async Task<Results<Created<VaccineRecordResponse>, ProblemHttpResult>> AddCaseVaccine(
        VaccineQueries vaccines,
        int personConditionId,
        SaveVaccineRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotAVaccineRecord(request) is { } why)
        {
            return Refused(why);
        }

        if (!await vaccines.CaseExists(personConditionId, cancellationToken))
        {
            return NoSuchCase(personConditionId);
        }

        if (!await vaccines.VaccineExists(request.VaccineId, cancellationToken))
        {
            return NoSuchVaccine(request.VaccineId);
        }

        var created = await vaccines.Add(personConditionId, request, cancellationToken);

        return TypedResults.Created($"/api/cases/vaccines/{created.Id}", created);
    }

    private static async Task<Results<Ok<VaccineRecordResponse>, ProblemHttpResult>> UpdateCaseVaccine(
        VaccineQueries vaccines,
        int id,
        SaveVaccineRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotAVaccineRecord(request) is { } why)
        {
            return Refused(why);
        }

        if (!await vaccines.VaccineExists(request.VaccineId, cancellationToken))
        {
            return NoSuchVaccine(request.VaccineId);
        }

        if (await vaccines.Update(id, request, cancellationToken) is not { } record)
        {
            return NoSuchRecord(id);
        }

        return TypedResults.Ok(record);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteCaseVaccine(
        VaccineQueries vaccines,
        int id,
        CancellationToken cancellationToken = default) =>
        await vaccines.Delete(id, cancellationToken)
            ? TypedResults.NoContent()
            : NoSuchRecord(id);

    private static ProblemHttpResult NoSuchCase(int personConditionId) =>
        TypedResults.Problem(
            detail: $"No case has id {personConditionId}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such case.");

    private static ProblemHttpResult NoSuchVaccine(int vaccineId) =>
        TypedResults.Problem(
            detail: $"No vaccine has id {vaccineId}. The vaccines are the ones GET /api/vaccines "
                + "lists.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such vaccine.");

    private static ProblemHttpResult NoSuchRecord(int id) =>
        TypedResults.Problem(
            detail: $"No vaccine record has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such vaccine record.");

    private static ProblemHttpResult Refused(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The vaccine record asked for is not one this endpoint can save.");
}

/// <summary>
/// The vaccine queries, and the only thing in this tier that holds the
/// <c>DbContext</c> for the vaccine catalogue and a case's vaccine records.
/// </summary>
/// <remarks>
/// Internal, as every slice's queries are. It reads the catalogue and the cases
/// directly rather than through their slices' queries: it holds the one
/// <c>DbContext</c>, and a cross-slice call would buy nothing but a second hop
/// to the same tables.
/// </remarks>
internal sealed class VaccineQueries(SurvsysDbContext database)
{
    /// <summary>The vaccine catalogue, in its seeded sort order and then by id.</summary>
    public async Task<IReadOnlyList<VaccineResponse>> Catalogue(CancellationToken cancellationToken) =>
        await database.Vaccines
            .OrderBy(vaccine => vaccine.SortOrder)
            .ThenBy(vaccine => vaccine.Id)
            .Select(VaccineEndpoints.ToResponse)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// A case's vaccine records, newest administered first and then by id; null
    /// where no case has the id.
    /// </summary>
    public async Task<IReadOnlyList<VaccineRecordResponse>?> Records(
        int personConditionId,
        CancellationToken cancellationToken)
    {
        if (!await CaseExists(personConditionId, cancellationToken))
        {
            return null;
        }

        var rows = await (
            from record in database.PersonVaccines.Where(record =>
                record.PersonConditionId == personConditionId)
            join vaccine in database.Vaccines on record.VaccineId equals vaccine.Id
            orderby record.AdministeredDate descending, record.Id descending
            select new RecordWithVaccine(record, vaccine.Description))
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => VaccineEndpoints.RespondRecord(row.Record, row.VaccineDescription))
            .ToList();
    }

    /// <summary>
    /// Records a vaccine against the case, stamping the moment, and answers it.
    /// The caller has proven the case and the vaccine exist.
    /// </summary>
    public async Task<VaccineRecordResponse> Add(
        int personConditionId,
        SaveVaccineRecordRequest request,
        CancellationToken cancellationToken)
    {
        var record = new PersonVaccine
        {
            PersonConditionId = personConditionId,
            VaccineId = request.VaccineId,
            AdministeredDate = request.AdministeredDate,
            DoseNumber = request.DoseNumber,
            LotNumber = Text(request.LotNumber),
            ExpirationDate = request.ExpirationDate,
            Comment = Text(request.Comment),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        database.PersonVaccines.Add(record);
        await database.SaveChangesAsync(cancellationToken);

        return await Respond(record, cancellationToken);
    }

    /// <summary>
    /// Saves over the record, or answers null where no record has that id. The
    /// caller has proven the vaccine exists.
    /// </summary>
    public async Task<VaccineRecordResponse?> Update(
        int id,
        SaveVaccineRecordRequest request,
        CancellationToken cancellationToken)
    {
        var record = await database.PersonVaccines
            .SingleOrDefaultAsync(record => record.Id == id, cancellationToken);

        if (record is null)
        {
            return null;
        }

        record.VaccineId = request.VaccineId;
        record.AdministeredDate = request.AdministeredDate;
        record.DoseNumber = request.DoseNumber;
        record.LotNumber = Text(request.LotNumber);
        record.ExpirationDate = request.ExpirationDate;
        record.Comment = Text(request.Comment);
        await database.SaveChangesAsync(cancellationToken);

        return await Respond(record, cancellationToken);
    }

    /// <summary>Deletes the record and answers whether one was there to delete.</summary>
    public async Task<bool> Delete(int id, CancellationToken cancellationToken) =>
        await database.PersonVaccines
            .Where(record => record.Id == id)
            .ExecuteDeleteAsync(cancellationToken) > 0;

    /// <summary>Whether a case this tier knows has that id.</summary>
    public Task<bool> CaseExists(int id, CancellationToken cancellationToken) =>
        database.PersonConditions.AnyAsync(report => report.Id == id, cancellationToken);

    /// <summary>Whether a vaccine the catalogue holds has that id.</summary>
    public Task<bool> VaccineExists(int id, CancellationToken cancellationToken) =>
        database.Vaccines.AnyAsync(vaccine => vaccine.Id == id, cancellationToken);

    private async Task<VaccineRecordResponse> Respond(
        PersonVaccine record,
        CancellationToken cancellationToken)
    {
        var description = await database.Vaccines
            .Where(vaccine => vaccine.Id == record.VaccineId)
            .Select(vaccine => vaccine.Description)
            .SingleAsync(cancellationToken);

        return VaccineEndpoints.RespondRecord(record, description);
    }

    private static string? Text(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>A record and its vaccine's description, carried out of a query.</summary>
    private sealed record RecordWithVaccine(PersonVaccine Record, string? VaccineDescription);
}
