using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>GET /api/conditions</c>: the conditions reference table, paged.
/// </summary>
/// <remarks>
/// The entity-to-wire mapping lives here because <c>Survsys.Data</c> may not
/// reference <c>Survsys.Contracts</c> — persistence must not know the wire
/// shapes — which leaves the translation to the tier that owns the wire.
///
/// The handler is handed <see cref="ConditionQueries"/> and not the
/// <c>DbContext</c>, because the architecture test in <c>Survsys.UnitTests</c>
/// lets no <c>Survsys.Data</c> type into an endpoint signature at all. That is
/// stricter than "no entity on the wire" and deliberately so: the database
/// stays one step behind a type this tier owns, and the query that reaches it
/// is a thing with a name rather than a lambda in a route registration.
/// </remarks>
public static class ConditionEndpoints
{
    /// <summary>
    /// The projection from the stored row to the wire shape — four of the
    /// table's eight columns.
    /// </summary>
    /// <remarks>
    /// An <see cref="Expression"/> rather than a method, so that it is the one
    /// mapping in both directions it has to work: EF Core translates it into
    /// the <c>SELECT</c> list, so the three lead-in prose columns are never
    /// read out of the database at all, and a unit test compiles and invokes
    /// the same expression to prove what it carries. A method would have made
    /// the test exercise something the query did not use.
    ///
    /// Public because the unit suite proves it. Nothing else calls it.
    /// </remarks>
    public static readonly Expression<Func<Condition, ConditionResponse>> ToResponse =
        condition => new ConditionResponse(
            condition.Id,
            condition.Name,
            condition.CdcCode,
            condition.Active);

    /// <summary>
    /// The projection from the stored row to the administration screen's shape
    /// — every column but the group. Public because the unit suite proves it.
    /// </summary>
    public static readonly Expression<Func<Condition, ConditionDetailResponse>> ToDetail =
        condition => new ConditionDetailResponse(
            condition.Id,
            condition.Name,
            condition.CdcCode,
            condition.Active,
            condition.ContactLeadIn,
            condition.PlaceLeadIn,
            condition.TreatmentLeadIn);

    /// <summary>
    /// <see cref="ToDetail"/> compiled, for a condition just saved and so in
    /// hand rather than in a query.
    /// </summary>
    public static readonly Func<Condition, ConditionDetailResponse> Detail = ToDetail.Compile();

    /// <summary>
    /// Why this is not a condition this tier will save, or null when it is
    /// one. A pure function the unit suite proves, with the endpoint turning
    /// the sentence into a 400; the bounds are the columns'.
    /// </summary>
    public static string? WhyNotACondition(SaveConditionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "name is required: a condition is listed and chosen by its name.";
        }

        if (request.Name.Trim().Length > NameLength)
        {
            return $"name is longer than {NameLength} characters.";
        }

        if (request.CdcCode?.Trim().Length > CdcCodeLength)
        {
            return $"cdcCode is longer than {CdcCodeLength} characters.";
        }

        return null;
    }

    private const int NameLength = 200;

    private const int CdcCodeLength = 20;

    /// <summary>
    /// Registers what the conditions endpoint needs of the container. Kept
    /// here with the endpoint so the slice's wiring is in one place, and so
    /// that <c>Program.cs</c> need not name an internal type.
    /// </summary>
    public static IServiceCollection AddConditions(this IServiceCollection services) =>
        services.AddScoped<ConditionQueries>();

    public static IEndpointRouteBuilder MapConditions(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/conditions", GetConditions)
            .WithName("GetConditions")
            .WithSummary("The conditions a case can be reported for, paged and ordered by name.");

        routes.MapGet("/api/conditions/{id:int}", GetCondition)
            .WithName("GetCondition")
            .WithSummary("One condition, with its investigator lead-in prose.");

        routes.MapPost("/api/conditions", CreateCondition)
            .WithName("CreateCondition")
            .WithSummary("Creates a condition.");

        routes.MapPut("/api/conditions/{id:int}", UpdateCondition)
            .WithName("UpdateCondition")
            .WithSummary("Saves over a condition.");

        routes.MapGet("/api/conditions/csv", async (ConditionQueries conditions, CancellationToken cancellationToken) =>
                TypedResults.Text(await conditions.Export(cancellationToken), "text/csv; charset=utf-8"))
            .WithName("ExportConditions")
            .WithSummary("Every condition, with its lead-in prose, as CSV.");

        routes.MapPost("/api/conditions/csv", async (
                ConditionQueries conditions,
                HttpRequest request,
                CancellationToken cancellationToken,
                bool confirm = false) =>
            {
                using var reader = new StreamReader(request.Body);
                var csv = await reader.ReadToEndAsync(cancellationToken);
                return TypedResults.Ok(await conditions.Import(csv, confirm, cancellationToken));
            })
            .WithName("ImportConditions")
            .WithSummary("Previews, or with confirm=true saves, each condition of a CSV file, added where its id "
                + "is blank and saved over by its id otherwise.");

        routes.MapGet("/api/conditions/{id:int}/print-criteria", GetPrintCriteria)
            .WithName("GetConditionPrintCriteria")
            .WithSummary("The case statuses that make a condition's cases notifiable to CDC.");

        routes.MapPut("/api/conditions/{id:int}/print-criteria", SetPrintCriteria)
            .WithName("SetConditionPrintCriteria")
            .WithSummary("Replaces the case statuses that make a condition's cases notifiable to CDC.");

        routes.MapGet("/api/conditions/{id:int}/lab-overrides", GetLabOverrides)
            .WithName("GetConditionLabOverrides")
            .WithSummary("The organisms and test types a condition's notification adds to or takes out of its labs.");

        routes.MapPut("/api/conditions/{id:int}/lab-overrides", SetLabOverrides)
            .WithName("SetConditionLabOverrides")
            .WithSummary("Replaces a condition's organism and test-type overrides.");

        return routes;
    }

    /// <summary>
    /// Why these are not a condition's lab overrides this tier will save, or
    /// null when they are: each names a kind, organism or test_type, and an
    /// item, and overrides an item once.
    /// </summary>
    public static string? WhyNotLabOverrides(IReadOnlyList<ConditionLabOverrideRow> overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);

        foreach (var item in overrides)
        {
            if (item is null)
            {
                return "an override is empty.";
            }

            if (item.Kind is not (ConditionLabOverride.OrganismKind or ConditionLabOverride.TestTypeKind))
            {
                return $"kind '{item.Kind}' is not {ConditionLabOverride.OrganismKind} or {ConditionLabOverride.TestTypeKind}.";
            }

            if (item.ItemId <= 0)
            {
                return "itemId is required: the organism or test type overridden.";
            }
        }

        return overrides.GroupBy(item => (item.Kind, item.ItemId)).FirstOrDefault(group => group.Count() > 1) is { } twice
            ? $"the {twice.Key.Kind} {twice.Key.ItemId} is overridden twice."
            : null;
    }

    private static async Task<Results<Ok<IReadOnlyList<ConditionLabOverrideRow>>, ProblemHttpResult>> GetLabOverrides(
        ConditionQueries conditions,
        int id,
        CancellationToken cancellationToken = default) =>
        await conditions.LabOverrides(id, cancellationToken) is { } overrides
            ? TypedResults.Ok(overrides)
            : NoSuchCondition(id);

    private static async Task<Results<Ok<IReadOnlyList<ConditionLabOverrideRow>>, ProblemHttpResult>> SetLabOverrides(
        ConditionQueries conditions,
        int id,
        IReadOnlyList<ConditionLabOverrideRow> overrides,
        CancellationToken cancellationToken = default)
    {
        if (!await conditions.Exists(id, cancellationToken))
        {
            return NoSuchCondition(id);
        }

        if ((WhyNotLabOverrides(overrides) ?? await conditions.WhyNotResolved(overrides, cancellationToken)) is { } why)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The lab overrides asked for are not ones this endpoint can save.");
        }

        return TypedResults.Ok((await conditions.SetLabOverrides(id, overrides, cancellationToken))!);
    }

    private static async Task<Results<Ok<ConditionPrintCriteriaResponse>, ProblemHttpResult>> GetPrintCriteria(
        ConditionQueries conditions,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (await conditions.PrintCriteria(id, cancellationToken) is not { } criteria)
        {
            return NoSuchCondition(id);
        }

        return TypedResults.Ok(criteria);
    }

    private static async Task<Results<Ok<ConditionPrintCriteriaResponse>, ProblemHttpResult>> SetPrintCriteria(
        ConditionQueries conditions,
        int id,
        ConditionPrintCriteriaRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.CaseStatuses is null)
        {
            return TypedResults.Problem(
                detail: "caseStatuses is required: the list of notifiable case status codes, empty for none.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "The print criteria asked for are not ones this endpoint can save.");
        }

        if (request.CaseStatuses.FirstOrDefault(code =>
            string.IsNullOrWhiteSpace(code) || !CaseStatusRules.IsAssignable(code)) is { } bad)
        {
            return TypedResults.Problem(
                detail: $"'{bad}' is not a case status code; a print criterion is one of "
                    + $"{CaseStatusRules.ConfirmedCode}, {CaseStatusRules.ProbableCode}, "
                    + $"{CaseStatusRules.SuspectedCode}, {CaseStatusRules.NotACaseCode} or "
                    + $"{CaseStatusRules.OutOfStateCode}.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "The print criteria asked for are not ones this endpoint can save.");
        }

        if (await conditions.SetPrintCriteria(id, request.CaseStatuses, cancellationToken) is not { } criteria)
        {
            return NoSuchCondition(id);
        }

        return TypedResults.Ok(criteria);
    }

    /// <summary>The conditions' CSV columns, in the order an export writes them.</summary>
    public static readonly IReadOnlyList<string> CsvColumns =
        ["id", "name", "active", "cdc_code", "contact_lead_in", "place_lead_in", "treatment_lead_in"];

    /// <summary>A condition as its CSV fields, in <see cref="CsvColumns"/> order; an id of 0 is blank.</summary>
    public static IReadOnlyList<string?> CsvFields(ConditionDetailResponse row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return
        [
            ReferenceCsv.Number(row.Id == 0 ? null : row.Id), row.Name, ReferenceCsv.YesNo(row.Active), row.CdcCode,
            row.ContactLeadIn, row.PlaceLeadIn, row.TreatmentLeadIn,
        ];
    }

    /// <summary>
    /// A condition read from its CSV row, a blank id read as 0 — a condition
    /// to add — or why it cannot be read.
    /// </summary>
    public static (ConditionDetailResponse? Row, string? Why) FromCsv(CsvRow row)
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

        return (new ConditionDetailResponse(
            id ?? 0,
            row.Value("name") ?? "",
            row.Value("cdc_code"),
            active,
            row.Value("contact_lead_in"),
            row.Value("place_lead_in"),
            row.Value("treatment_lead_in")), null);
    }

    /// <summary>A condition row as the request that saves it.</summary>
    public static SaveConditionRequest Request(ConditionDetailResponse row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return new(row.Name, row.Active, row.CdcCode, row.ContactLeadIn, row.PlaceLeadIn, row.TreatmentLeadIn);
    }

    private static async Task<Results<Ok<ConditionDetailResponse>, ProblemHttpResult>> GetCondition(
        ConditionQueries conditions,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (await conditions.Find(id, cancellationToken) is not { } condition)
        {
            return NoSuchCondition(id);
        }

        return TypedResults.Ok(condition);
    }

    private static async Task<Results<Created<ConditionDetailResponse>, ProblemHttpResult>> CreateCondition(
        ConditionQueries conditions,
        SaveConditionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotACondition(request) is { } why)
        {
            return Refused(why);
        }

        var created = await conditions.Add(request, cancellationToken);

        return TypedResults.Created($"/api/conditions/{created.Id}", created);
    }

    private static async Task<Results<Ok<ConditionDetailResponse>, ProblemHttpResult>> UpdateCondition(
        ConditionQueries conditions,
        int id,
        SaveConditionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotACondition(request) is { } why)
        {
            return Refused(why);
        }

        if (await conditions.Update(id, request, cancellationToken) is not { } condition)
        {
            return NoSuchCondition(id);
        }

        return TypedResults.Ok(condition);
    }

    private static ProblemHttpResult NoSuchCondition(int id) =>
        TypedResults.Problem(
            detail: $"No condition has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such condition.");

    private static ProblemHttpResult Refused(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The condition asked for is not one this endpoint can save.");

    private static async Task<Results<Ok<PagedResult<ConditionResponse>>, ProblemHttpResult>> GetConditions(
        ConditionQueries conditions,
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

        return TypedResults.Ok(await conditions.Page(page, pageSize, cancellationToken));
    }
}

/// <summary>
/// The conditions query, and the only thing in this tier that holds the
/// <c>DbContext</c> for them.
/// </summary>
/// <remarks>
/// Internal, and in this file rather than its own, because it exists only to
/// serve <see cref="ConditionEndpoints"/>: it is the named step between an
/// endpoint signature and <c>Survsys.Data</c> that the architecture test
/// requires, and it keeps the ordering and the paging arithmetic in one place
/// instead of in a route registration.
///
/// Nothing here translates a database failure into an HTTP concern. An EF Core
/// or SQL Server exception surfaces unwrapped and the exception handler turns
/// it into a 500 with Problem Details and a correlation id.
/// </remarks>
internal sealed class ConditionQueries(SurvsysDbContext database)
{
    /// <summary>
    /// One page of conditions, ordered by name and then by id — by id as well
    /// because names are not unique in the legacy data, and an order that is
    /// not total lets a row appear on two pages or on neither.
    /// </summary>
    public async Task<PagedResult<ConditionResponse>> Page(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var total = await database.Conditions.CountAsync(cancellationToken);

        var rows = await database.Conditions
            .OrderBy(condition => condition.Name)
            .ThenBy(condition => condition.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ConditionEndpoints.ToResponse)
            .ToListAsync(cancellationToken);

        return new PagedResult<ConditionResponse>(rows, page, pageSize, total);
    }

    /// <summary>The condition with that id, or null where no condition has it.</summary>
    public Task<ConditionDetailResponse?> Find(int id, CancellationToken cancellationToken) =>
        database.Conditions
            .Where(condition => condition.Id == id)
            .Select(ConditionEndpoints.ToDetail)
            .SingleOrDefaultAsync(cancellationToken);

    public static readonly ReferenceTable<Condition, ConditionDetailResponse> Table = new ConditionTable();

    public Task<string> Export(CancellationToken cancellationToken) =>
        ReferenceImport.Export(database, Table, cancellationToken);

    /// <summary>What importing a CSV file would do, written only where <paramref name="confirm"/> is true.</summary>
    public Task<ReferenceImportResponse> Import(string? csv, bool confirm, CancellationToken cancellationToken) =>
        ReferenceImport.Import(database, Table, csv, confirm, cancellationToken);

    public async Task<ConditionDetailResponse> Add(SaveConditionRequest request, CancellationToken cancellationToken)
    {
        var condition = new Condition { Name = request.Name };
        Apply(condition, request);
        database.Conditions.Add(condition);
        await database.SaveChangesAsync(cancellationToken);

        return ConditionEndpoints.Detail(condition);
    }

    /// <summary>
    /// Saves over the condition, or answers null where no condition has that
    /// id. The group is left as it is: it is not on the wire.
    /// </summary>
    public async Task<ConditionDetailResponse?> Update(
        int id,
        SaveConditionRequest request,
        CancellationToken cancellationToken)
    {
        var condition = await database.Conditions
            .SingleOrDefaultAsync(condition => condition.Id == id, cancellationToken);

        if (condition is null)
        {
            return null;
        }

        Apply(condition, request);
        await database.SaveChangesAsync(cancellationToken);

        return ConditionEndpoints.Detail(condition);
    }

    /// <summary>
    /// The condition's print criteria, or null where no condition has that id.
    /// </summary>
    public async Task<ConditionPrintCriteriaResponse?> PrintCriteria(int id, CancellationToken cancellationToken)
    {
        if (!await database.Conditions.AnyAsync(condition => condition.Id == id, cancellationToken))
        {
            return null;
        }

        var statuses = await database.ConditionPrintCriteria
            .Where(criterion => criterion.ConditionId == id)
            .OrderBy(criterion => criterion.CaseStatus)
            .Select(criterion => criterion.CaseStatus)
            .ToListAsync(cancellationToken);

        return new ConditionPrintCriteriaResponse(id, statuses);
    }

    /// <summary>
    /// Replaces the condition's print criteria with <paramref name="statuses"/>,
    /// trimmed and each once, or answers null where no condition has that id.
    /// The codes are the caller's to have checked.
    /// </summary>
    public async Task<ConditionPrintCriteriaResponse?> SetPrintCriteria(
        int id,
        IEnumerable<string> statuses,
        CancellationToken cancellationToken)
    {
        if (!await database.Conditions.AnyAsync(condition => condition.Id == id, cancellationToken))
        {
            return null;
        }

        database.ConditionPrintCriteria.RemoveRange(
            await database.ConditionPrintCriteria.Where(criterion => criterion.ConditionId == id)
                .ToListAsync(cancellationToken));
        database.ConditionPrintCriteria.AddRange(statuses
            .Select(status => status.Trim())
            .Distinct(StringComparer.Ordinal)
            .Select(status => new ConditionPrintCriterion { ConditionId = id, CaseStatus = status }));
        await database.SaveChangesAsync(cancellationToken);

        return await PrintCriteria(id, cancellationToken);
    }

    public Task<bool> Exists(int id, CancellationToken cancellationToken) =>
        database.Conditions.AnyAsync(condition => condition.Id == id, cancellationToken);

    /// <summary>The condition's lab overrides, or null where no condition has that id.</summary>
    public async Task<IReadOnlyList<ConditionLabOverrideRow>?> LabOverrides(int id, CancellationToken cancellationToken)
    {
        if (!await Exists(id, cancellationToken))
        {
            return null;
        }

        return await database.ConditionLabOverrides
            .Where(item => item.ConditionId == id)
            .OrderBy(item => item.Kind).ThenBy(item => item.ItemId)
            .Select(item => new ConditionLabOverrideRow(item.Kind, item.ItemId, item.Include))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Which overridden organism or test type is not on file, or null.</summary>
    public async Task<string?> WhyNotResolved(
        IReadOnlyList<ConditionLabOverrideRow> overrides, CancellationToken cancellationToken)
    {
        var organisms = overrides.Where(item => item.Kind == ConditionLabOverride.OrganismKind)
            .Select(item => item.ItemId).ToList();
        var testTypes = overrides.Where(item => item.Kind == ConditionLabOverride.TestTypeKind)
            .Select(item => item.ItemId).ToList();
        var knownOrganisms = await database.VocabMasterOrganisms.Where(item => organisms.Contains(item.Id))
            .Select(item => item.Id).ToListAsync(cancellationToken);
        var knownTestTypes = await database.CodeValues
            .Where(item => item.List == CodeLists.TestType && testTypes.Contains(item.Id))
            .Select(item => item.Id).ToListAsync(cancellationToken);

        return organisms.Except(knownOrganisms).Select(item => (int?)item).FirstOrDefault() is { } organism
            ? $"organism {organism} is not a master organism GET /api/vocabulary/organisms lists."
            : testTypes.Except(knownTestTypes).Select(item => (int?)item).FirstOrDefault() is { } testType
                ? $"test type {testType} is not a {CodeLists.TestType} code GET /api/codes lists."
                : null;
    }

    /// <summary>Replaces the condition's lab overrides; the rows are the caller's to have checked.</summary>
    public async Task<IReadOnlyList<ConditionLabOverrideRow>?> SetLabOverrides(
        int id, IReadOnlyList<ConditionLabOverrideRow> overrides, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await database.ConditionLabOverrides.Where(item => item.ConditionId == id).ExecuteDeleteAsync(cancellationToken);
        database.ConditionLabOverrides.AddRange(overrides.Select(item => new ConditionLabOverride
        {
            ConditionId = id, Kind = item.Kind, ItemId = item.ItemId, Include = item.Include,
        }));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await LabOverrides(id, cancellationToken);
    }

    /// <summary>
    /// The request's values onto the condition: the name and code trimmed, and
    /// a blank optional value stored as null. The lead-in prose is kept as
    /// written, since its line breaks are the investigator's.
    /// </summary>
    internal static void Apply(Condition condition, SaveConditionRequest request)
    {
        condition.Name = request.Name.Trim();
        condition.Active = request.Active;
        condition.CdcCode = string.IsNullOrWhiteSpace(request.CdcCode) ? null : request.CdcCode.Trim();
        condition.ContactLeadIn = string.IsNullOrWhiteSpace(request.ContactLeadIn) ? null : request.ContactLeadIn;
        condition.PlaceLeadIn = string.IsNullOrWhiteSpace(request.PlaceLeadIn) ? null : request.PlaceLeadIn;
        condition.TreatmentLeadIn = string.IsNullOrWhiteSpace(request.TreatmentLeadIn) ? null : request.TreatmentLeadIn;
    }
}

/// <summary>
/// The conditions, keyed by id: a row whose id is blank is a condition to add,
/// and a row naming an id not on file is refused. A condition has no other
/// key — names are not unique in the legacy data. The group is not carried,
/// and is left as it is.
/// </summary>
internal sealed class ConditionTable : ReferenceTable<Condition, ConditionDetailResponse>
{
    public override IReadOnlyList<string> Columns => ConditionEndpoints.CsvColumns;

    public override Task<List<ConditionDetailResponse>> List(
        SurvsysDbContext database, CancellationToken cancellationToken) =>
        database.Conditions
            .OrderBy(condition => condition.Name)
            .ThenBy(condition => condition.Id)
            .Select(ConditionEndpoints.ToDetail)
            .ToListAsync(cancellationToken);

    public override IReadOnlyList<string?> Fields(ConditionDetailResponse row) => ConditionEndpoints.CsvFields(row);

    public override (ConditionDetailResponse? Row, string? Why) Parse(CsvRow row) => ConditionEndpoints.FromCsv(row);

    public override string? WhyNot(ConditionDetailResponse row) =>
        ConditionEndpoints.WhyNotACondition(ConditionEndpoints.Request(row));

    public override string KeyOf(ConditionDetailResponse row) => row.Id == 0 ? row.Name.Trim() : $"{row.Id}";

    public override async Task<Condition?> ByKey(
        SurvsysDbContext database, ConditionDetailResponse row, CancellationToken cancellationToken) =>
        row.Id == 0
            ? null
            : await database.Conditions.SingleOrDefaultAsync(condition => condition.Id == row.Id, cancellationToken);

    public override Task<ConditionDetailResponse> RowOf(
        SurvsysDbContext database, Condition entity, CancellationToken cancellationToken) =>
        Task.FromResult(ConditionEndpoints.Detail(entity));

    public override Condition New(ConditionDetailResponse row) => new() { Name = row.Name.Trim() };

    public override Task<string?> Apply(
        SurvsysDbContext database, Condition entity, ConditionDetailResponse row, CancellationToken cancellationToken)
    {
        if (row.Id != entity.Id)
        {
            return Task.FromResult<string?>(
                $"id {row.Id} is not a condition on file; leave id blank to add a condition.");
        }

        ConditionQueries.Apply(entity, ConditionEndpoints.Request(row));
        return Task.FromResult<string?>(null);
    }
}
