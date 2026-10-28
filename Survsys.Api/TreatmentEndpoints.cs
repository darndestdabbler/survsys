using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>/api/treatments</c>: the treatment catalogue and the condition-specific
/// lists — which treatments a condition offers — that drive what an
/// investigator is shown for a case's disease — and, under
/// <c>/api/cases</c>, the treatments recorded against a case.
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

    private const int DoseLength = 100;

    private const int CommentLength = 1_000;

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
    /// Why this is not a case treatment record this tier will save, or null
    /// when it is one.
    /// </summary>
    /// <remarks>
    /// Pure, as <see cref="VaccineEndpoints.WhyNotAVaccineRecord"/> is: whether
    /// the treatment is in the catalogue is the database's to answer.
    /// </remarks>
    public static string? WhyNotATreatmentRecord(SaveTreatmentRecordRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.TreatmentId < 1)
        {
            return "treatmentId is required: a record names the treatment given, chosen from "
                + "the ones GET /api/treatments lists.";
        }

        if (request.TreatmentDate is { } started
            && request.StopTreatmentDate is { } stopped
            && stopped < started)
        {
            return "stopTreatmentDate is before treatmentDate: a treatment does not stop before "
                + "it starts.";
        }

        if (request.Dose?.Trim().Length > DoseLength)
        {
            return $"dose is longer than {DoseLength} characters.";
        }

        if (request.Comment?.Trim().Length > CommentLength)
        {
            return $"comment is longer than {CommentLength} characters.";
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

    /// <summary>The treatments' CSV columns, in the order an export writes them.</summary>
    public static readonly IReadOnlyList<string> CsvColumns =
        ["id", "name", "active", "default_treatment", "condition_ids"];

    /// <summary>
    /// A treatment as its CSV fields, in <see cref="CsvColumns"/> order: an id
    /// of 0 blank, and the conditions as ids in order joined by <c>;</c>.
    /// </summary>
    public static IReadOnlyList<string?> CsvFields(TreatmentCsvRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return
        [
            ReferenceCsv.Number(row.Id == 0 ? null : row.Id), row.Name, ReferenceCsv.YesNo(row.Active),
            ReferenceCsv.YesNo(row.DefaultTreatment), string.Join(";", row.ConditionIds.Order()),
        ];
    }

    /// <summary>
    /// A treatment read from its CSV row, a blank id read as 0 — a treatment
    /// to add — or why it cannot be read.
    /// </summary>
    public static (TreatmentCsvRow? Row, string? Why) FromCsv(CsvRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (ReferenceCsv.Optional(row, "id", out var id) is { } badId)
        {
            return (null, badId);
        }

        if (ReferenceCsv.Flag(row, "active", out var active) is { } badActive)
        {
            return (null, badActive);
        }

        if (ReferenceCsv.Flag(row, "default_treatment", out var defaultTreatment) is { } badDefault)
        {
            return (null, badDefault);
        }

        var conditionIds = new List<int>();

        foreach (var text in (row.Value("condition_ids") ?? "")
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var conditionId))
            {
                return (null, $"condition_ids '{text}' is not a whole number.");
            }

            conditionIds.Add(conditionId);
        }

        return (new TreatmentCsvRow(id ?? 0, row.Value("name") ?? "", active, defaultTreatment, conditionIds), null);
    }

    /// <summary>A treatment row as the request that saves it.</summary>
    public static SaveTreatmentRequest Request(TreatmentCsvRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return new(row.Name, row.Active, row.DefaultTreatment, row.ConditionIds);
    }

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

        routes.MapGet("/api/treatments/csv", async (TreatmentQueries treatments, CancellationToken cancellationToken) =>
                TypedResults.Text(await treatments.Export(cancellationToken), "text/csv; charset=utf-8"))
            .WithName("ExportTreatments")
            .WithSummary("Every treatment, with the conditions it is listed for, as CSV.");

        routes.MapPost("/api/treatments/csv", async (
                TreatmentQueries treatments,
                HttpRequest request,
                CancellationToken cancellationToken,
                bool confirm = false) =>
            {
                using var reader = new StreamReader(request.Body);
                var csv = await reader.ReadToEndAsync(cancellationToken);
                return TypedResults.Ok(await treatments.Import(csv, confirm, cancellationToken));
            })
            .WithName("ImportTreatments")
            .WithSummary("Previews, or with confirm=true saves, each treatment of a CSV file, added where its id "
                + "is blank and saved over by its id otherwise, its conditions replaced.");

        routes.MapGet("/api/conditions/{conditionId:int}/treatments", GetConditionTreatments)
            .WithName("GetConditionTreatments")
            .WithSummary("The treatments a condition is listed for, paged and ordered by name.");

        routes.MapGet("/api/cases/{personConditionId:int}/treatments", GetCaseTreatments)
            .WithName("GetCaseTreatments")
            .WithSummary("A case's treatment records, newest started first.");

        routes.MapPost("/api/cases/{personConditionId:int}/treatments", AddCaseTreatment)
            .WithName("AddCaseTreatment")
            .WithSummary("Records a treatment given to a case.");

        routes.MapPut("/api/cases/treatments/{id:int}", UpdateCaseTreatment)
            .WithName("UpdateCaseTreatment")
            .WithSummary("Saves over a case's treatment record.");

        routes.MapDelete("/api/cases/treatments/{id:int}", DeleteCaseTreatment)
            .WithName("DeleteCaseTreatment")
            .WithSummary("Removes a case's treatment record.");

        return routes;
    }

    private static async Task<Results<Ok<IReadOnlyList<TreatmentRecordResponse>>, ProblemHttpResult>>
        GetCaseTreatments(
            TreatmentQueries treatments,
            int personConditionId,
            CancellationToken cancellationToken = default)
    {
        if (await treatments.Records(personConditionId, cancellationToken) is not { } records)
        {
            return NoSuchCase(personConditionId);
        }

        return TypedResults.Ok(records);
    }

    private static async Task<Results<Created<TreatmentRecordResponse>, ProblemHttpResult>> AddCaseTreatment(
        TreatmentQueries treatments,
        int personConditionId,
        SaveTreatmentRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotATreatmentRecord(request) is { } why)
        {
            return RefusedRecord(why);
        }

        if (!await treatments.CaseExists(personConditionId, cancellationToken))
        {
            return NoSuchCase(personConditionId);
        }

        if (!await treatments.TreatmentExists(request.TreatmentId, cancellationToken))
        {
            return RefusedRecord(NotATreatment(request.TreatmentId));
        }

        var created = await treatments.AddRecord(personConditionId, request, cancellationToken);

        return TypedResults.Created($"/api/cases/treatments/{created.Id}", created);
    }

    private static async Task<Results<Ok<TreatmentRecordResponse>, ProblemHttpResult>> UpdateCaseTreatment(
        TreatmentQueries treatments,
        int id,
        SaveTreatmentRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotATreatmentRecord(request) is { } why)
        {
            return RefusedRecord(why);
        }

        if (!await treatments.TreatmentExists(request.TreatmentId, cancellationToken))
        {
            return RefusedRecord(NotATreatment(request.TreatmentId));
        }

        if (await treatments.UpdateRecord(id, request, cancellationToken) is not { } record)
        {
            return NoSuchRecord(id);
        }

        return TypedResults.Ok(record);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteCaseTreatment(
        TreatmentQueries treatments,
        int id,
        CancellationToken cancellationToken = default) =>
        await treatments.DeleteRecord(id, cancellationToken)
            ? TypedResults.NoContent()
            : NoSuchRecord(id);

    private static string NotATreatment(int treatmentId) =>
        $"treatmentId {treatmentId} is not a treatment the catalogue holds. The treatments are "
            + "the ones GET /api/treatments lists.";

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

    private static ProblemHttpResult NoSuchCase(int personConditionId) =>
        TypedResults.Problem(
            detail: $"No case has id {personConditionId}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such case.");

    private static ProblemHttpResult NoSuchRecord(int id) =>
        TypedResults.Problem(
            detail: $"No treatment record has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such treatment record.");

    private static ProblemHttpResult RefusedRecord(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The treatment record asked for is not one this endpoint can save.");
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

    /// <summary>
    /// A case's treatment records, newest started first and then by id; null
    /// where no case has the id.
    /// </summary>
    public async Task<IReadOnlyList<TreatmentRecordResponse>?> Records(
        int personConditionId,
        CancellationToken cancellationToken)
    {
        if (!await CaseExists(personConditionId, cancellationToken))
        {
            return null;
        }

        return await (
            from record in database.PersonTreatments.Where(record =>
                record.PersonConditionId == personConditionId)
            join treatment in database.Treatments on record.TreatmentId equals treatment.Id
            orderby record.TreatmentDate descending, record.Id descending
            select new TreatmentRecordResponse(
                record.Id,
                record.TreatmentId,
                treatment.Name,
                record.TreatmentDate,
                record.StopTreatmentDate,
                record.Dose,
                record.Comment))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Records a treatment against the case, stamping the moment, and answers
    /// it. The caller has proven the case and the treatment exist.
    /// </summary>
    public async Task<TreatmentRecordResponse> AddRecord(
        int personConditionId,
        SaveTreatmentRecordRequest request,
        CancellationToken cancellationToken)
    {
        var record = new PersonTreatment
        {
            PersonConditionId = personConditionId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        Fill(record, request);
        database.PersonTreatments.Add(record);
        await database.SaveChangesAsync(cancellationToken);

        return await RespondRecord(record, cancellationToken);
    }

    /// <summary>
    /// Saves over the record, or answers null where no record has that id. The
    /// caller has proven the treatment exists.
    /// </summary>
    public async Task<TreatmentRecordResponse?> UpdateRecord(
        int id,
        SaveTreatmentRecordRequest request,
        CancellationToken cancellationToken)
    {
        var record = await database.PersonTreatments
            .SingleOrDefaultAsync(record => record.Id == id, cancellationToken);

        if (record is null)
        {
            return null;
        }

        Fill(record, request);
        await database.SaveChangesAsync(cancellationToken);

        return await RespondRecord(record, cancellationToken);
    }

    /// <summary>Deletes the record and answers whether one was there to delete.</summary>
    public async Task<bool> DeleteRecord(int id, CancellationToken cancellationToken) =>
        await database.PersonTreatments
            .Where(record => record.Id == id)
            .ExecuteDeleteAsync(cancellationToken) > 0;

    /// <summary>Whether a case this tier knows has that id.</summary>
    public Task<bool> CaseExists(int id, CancellationToken cancellationToken) =>
        database.PersonConditions.AnyAsync(report => report.Id == id, cancellationToken);

    /// <summary>Whether a treatment the catalogue holds has that id.</summary>
    public Task<bool> TreatmentExists(int id, CancellationToken cancellationToken) =>
        database.Treatments.AnyAsync(treatment => treatment.Id == id, cancellationToken);

    private static void Fill(PersonTreatment record, SaveTreatmentRecordRequest request)
    {
        record.TreatmentId = request.TreatmentId;
        record.TreatmentDate = request.TreatmentDate;
        record.StopTreatmentDate = request.StopTreatmentDate;
        record.Dose = Text(request.Dose);
        record.Comment = Text(request.Comment);
    }

    private async Task<TreatmentRecordResponse> RespondRecord(
        PersonTreatment record,
        CancellationToken cancellationToken)
    {
        var name = await database.Treatments
            .Where(treatment => treatment.Id == record.TreatmentId)
            .Select(treatment => treatment.Name)
            .SingleAsync(cancellationToken);

        return new TreatmentRecordResponse(
            record.Id,
            record.TreatmentId,
            name,
            record.TreatmentDate,
            record.StopTreatmentDate,
            record.Dose,
            record.Comment);
    }

    private static string? Text(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private Task<List<TreatmentConditionResponse>> ConditionRows(
        int treatmentId,
        CancellationToken cancellationToken) =>
        database.ConditionTreatments
            .Where(link => link.TreatmentId == treatmentId)
            .Join(
                database.Conditions,
                link => link.ConditionId,
                condition => condition.Id,
                (link, condition) => condition)
            .OrderBy(condition => condition.Name)
            .ThenBy(condition => condition.Id)
            .Select(condition => new TreatmentConditionResponse(condition.Id, condition.Name))
            .ToListAsync(cancellationToken);

    /// <summary>A treatment and the count of conditions it is listed for, carried out of a query.</summary>
    private sealed record TreatmentWithCount(Treatment Treatment, int ConditionCount);

    public static readonly ReferenceTable<Treatment, TreatmentCsvRow> Table = new TreatmentTable();

    public Task<string> Export(CancellationToken cancellationToken) =>
        ReferenceImport.Export(database, Table, cancellationToken);

    /// <summary>What importing a CSV file would do, written only where <paramref name="confirm"/> is true.</summary>
    public Task<ReferenceImportResponse> Import(string? csv, bool confirm, CancellationToken cancellationToken) =>
        ReferenceImport.Import(database, Table, csv, confirm, cancellationToken);
}

/// <summary>
/// One row of the treatments' CSV file: a treatment and the ids of the
/// conditions it is listed for. An id of 0 is a treatment to add.
/// </summary>
public sealed record TreatmentCsvRow(
    int Id, string Name, bool Active, bool DefaultTreatment, IReadOnlyList<int> ConditionIds);

/// <summary>
/// The treatments, keyed by id: a row whose id is blank is a treatment to add,
/// and a row naming an id not on file is refused. A treatment has no other key
/// — two may share a name. A row's conditions replace the ones the treatment
/// is listed for.
/// </summary>
internal sealed class TreatmentTable : ReferenceTable<Treatment, TreatmentCsvRow>
{
    public override IReadOnlyList<string> Columns => TreatmentEndpoints.CsvColumns;

    public override async Task<List<TreatmentCsvRow>> List(SurvsysDbContext database, CancellationToken cancellationToken)
    {
        var treatments = await database.Treatments
            .OrderBy(treatment => treatment.Name)
            .ThenBy(treatment => treatment.Id)
            .ToListAsync(cancellationToken);
        var links = (await database.ConditionTreatments.ToListAsync(cancellationToken))
            .ToLookup(link => link.TreatmentId, link => link.ConditionId);

        return treatments.Select(treatment => Row(treatment, links[treatment.Id].ToList())).ToList();
    }

    public override IReadOnlyList<string?> Fields(TreatmentCsvRow row) => TreatmentEndpoints.CsvFields(row);

    public override (TreatmentCsvRow? Row, string? Why) Parse(CsvRow row) => TreatmentEndpoints.FromCsv(row);

    public override string? WhyNot(TreatmentCsvRow row) =>
        TreatmentEndpoints.WhyNotATreatment(TreatmentEndpoints.Request(row));

    public override string KeyOf(TreatmentCsvRow row) => row.Id == 0 ? row.Name.Trim() : $"{row.Id}";

    public override async Task<Treatment?> ByKey(
        SurvsysDbContext database, TreatmentCsvRow row, CancellationToken cancellationToken) =>
        row.Id == 0
            ? null
            : await database.Treatments.SingleOrDefaultAsync(treatment => treatment.Id == row.Id, cancellationToken);

    public override async Task<TreatmentCsvRow> RowOf(
        SurvsysDbContext database, Treatment entity, CancellationToken cancellationToken) =>
        Row(entity, await database.ConditionTreatments
            .Where(link => link.TreatmentId == entity.Id)
            .Select(link => link.ConditionId)
            .ToListAsync(cancellationToken));

    public override Treatment New(TreatmentCsvRow row) =>
        new() { Name = row.Name.Trim(), CreatedAt = DateTimeOffset.UtcNow };

    public override async Task<string?> Apply(
        SurvsysDbContext database, Treatment entity, TreatmentCsvRow row, CancellationToken cancellationToken)
    {
        if (row.Id != entity.Id)
        {
            return $"id {row.Id} is not a treatment on file; leave id blank to add a treatment.";
        }

        var conditionIds = row.ConditionIds.ToList();
        var found = await database.Conditions
            .Where(condition => conditionIds.Contains(condition.Id))
            .Select(condition => condition.Id)
            .ToListAsync(cancellationToken);

        if (conditionIds.Except(found).Select(id => (int?)id).FirstOrDefault() is { } missing)
        {
            return TreatmentEndpoints.NotACondition(missing);
        }

        entity.Name = row.Name.Trim();
        entity.Active = row.Active;
        entity.DefaultTreatment = row.DefaultTreatment;
        return null;
    }

    public override async Task Saved(
        SurvsysDbContext database, Treatment entity, TreatmentCsvRow row, CancellationToken cancellationToken)
    {
        await database.ConditionTreatments
            .Where(link => link.TreatmentId == entity.Id)
            .ExecuteDeleteAsync(cancellationToken);
        database.ConditionTreatments.AddRange(row.ConditionIds.Distinct().Select(conditionId =>
            new ConditionTreatment { ConditionId = conditionId, TreatmentId = entity.Id }));
        await database.SaveChangesAsync(cancellationToken);
    }

    private static TreatmentCsvRow Row(Treatment treatment, IReadOnlyList<int> conditionIds) =>
        new(treatment.Id, treatment.Name, treatment.Active, treatment.DefaultTreatment, conditionIds);
}
