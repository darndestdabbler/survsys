using System.Globalization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>/api/codes</c>: the reference code lists every coded field is chosen
/// from — one list read, and a code added or saved over.
/// </summary>
/// <remarks>
/// One endpoint for every list rather than one per legacy table, because a
/// list is rows of one table told apart by name (<see cref="CodeLists"/>).
/// A code is never deleted: it is retired by saving it inactive, so a record
/// that names it still does, and the default read leaves it out.
///
/// The test types are the one list mapped to conditions: read with a
/// condition, the list is narrowed to the test types mapped to it, or is the
/// whole list where that condition has none mapped, so a condition nobody has
/// mapped yet still offers every test type.
///
/// Each list is exported and imported as CSV by <see cref="ReferenceImport"/>,
/// a row matched to a code by the code itself: the county list carries each
/// county's state, FIPS code and kind, and the test types their conditions.
///
/// The handlers are handed <see cref="CodeQueries"/> and not the
/// <c>DbContext</c>, because the architecture test lets no <c>Survsys.Data</c>
/// type into an endpoint signature.
/// </remarks>
public static class CodeEndpoints
{
    private const int CodeLength = 100;

    private const int DescriptionLength = 400;

    private const int StateLength = 2;

    private const int FipsLength = 5;

    /// <summary>
    /// Why this is not a code this tier will save on that list, as far as can
    /// be said without the database, or null when it is one.
    /// </summary>
    /// <remarks>
    /// A pure function the unit suite proves, with the endpoint turning the
    /// sentence into a 400. Whether the code is taken and whether the
    /// conditions exist need a query and are the endpoint's.
    /// </remarks>
    public static string? WhyNotACode(string list, SaveCodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsAList(list))
        {
            return NotAList(list);
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return "code is required: a coded field stores the code.";
        }

        if (request.ConditionIds is { Count: > 0 } && list != CodeLists.TestType)
        {
            return $"conditionIds are only kept for the {CodeLists.TestType} list, not {list}.";
        }

        return TooLong("code", request.Code, CodeLength)
            ?? TooLong("description", request.Description, DescriptionLength);
    }

    /// <summary>
    /// The CSV columns of a list: the code, its description, sort order and
    /// whether it is active, then the county list's state, FIPS code and kind,
    /// or the test types' conditions.
    /// </summary>
    public static IReadOnlyList<string> CsvColumns(string list)
    {
        var columns = new List<string> { "code", "description", "sort_order", "active" };

        if (list == CodeLists.County)
        {
            columns.AddRange(["state", "fips", "kind"]);
        }
        else if (list == CodeLists.TestType)
        {
            columns.Add("condition_ids");
        }

        return columns;
    }

    /// <summary>A code's CSV fields, in <see cref="CsvColumns"/>'s order; conditions as ids joined by <c>;</c>.</summary>
    public static IReadOnlyList<string?> CsvFields(string list, CodeResponse code)
    {
        ArgumentNullException.ThrowIfNull(code);

        var fields = new List<string?>
        {
            code.Code, code.Description, ReferenceCsv.Number(code.SortOrder), ReferenceCsv.YesNo(code.Active),
        };

        if (list == CodeLists.County)
        {
            fields.AddRange([code.State, code.Fips, code.Kind]);
        }
        else if (list == CodeLists.TestType)
        {
            fields.Add(string.Join(";", code.ConditionIds.Order()));
        }

        return fields;
    }

    /// <summary>
    /// A code read from its CSV row, or why the row is not one: a blank sort
    /// order is 0 and a blank active is yes, so a new code needs only its code.
    /// </summary>
    public static (CodeResponse? Code, string? Why) FromCsv(string list, CsvRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (ReferenceCsv.Optional(row, "sort_order", out var sortOrder) is { } notANumber)
        {
            return (null, notANumber);
        }

        var active = true;
        if (row.Value("active") is not null && ReferenceCsv.Flag(row, "active", out active) is { } notAFlag)
        {
            return (null, notAFlag);
        }

        var conditionIds = new List<int>();
        var conditions = list == CodeLists.TestType ? row.Value("condition_ids") ?? "" : "";

        foreach (var text in conditions.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var conditionId))
            {
                return (null, $"condition_ids '{text}' is not a whole number.");
            }

            conditionIds.Add(conditionId);
        }

        var county = list == CodeLists.County;

        return (new CodeResponse(
            0,
            list,
            row.Value("code") ?? "",
            row.Value("description"),
            sortOrder ?? 0,
            active,
            conditionIds,
            county ? row.Value("state")?.ToUpperInvariant() : null,
            county ? row.Value("fips") : null,
            county ? row.Value("kind")?.ToLowerInvariant() : null), null);
    }

    /// <summary>
    /// Why a code read from CSV is not one this tier will save on that list:
    /// what <see cref="WhyNotACode"/> refuses, and on the county list a state
    /// or FIPS code too long for its column or a kind that is neither
    /// <c>county</c> nor <c>county-equivalent</c>.
    /// </summary>
    public static string? WhyNotACsvCode(string list, CodeResponse code)
    {
        ArgumentNullException.ThrowIfNull(code);

        return WhyNotACode(
                list, new SaveCodeRequest(code.Code, code.Description, code.SortOrder, code.Active, code.ConditionIds))
            ?? TooLong("state", code.State, StateLength)
            ?? TooLong("fips", code.Fips, FipsLength)
            ?? (code.Kind is null or CodeValue.CountyKind or CodeValue.CountyEquivalentKind
                ? null
                : $"kind '{code.Kind}' is not {CodeValue.CountyKind} or {CodeValue.CountyEquivalentKind}.");
    }

    /// <summary>
    /// Registers what the code endpoints need of the container.
    /// </summary>
    public static IServiceCollection AddCodes(this IServiceCollection services) =>
        services.AddScoped<CodeQueries>();

    public static IEndpointRouteBuilder MapCodes(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/codes/{list}", GetCodes)
            .WithName("GetCodes")
            .WithSummary("One code list, by sort order then description.");

        routes.MapPost("/api/codes/{list}", CreateCode)
            .WithName("CreateCode")
            .WithSummary("Adds a code to a list.");

        routes.MapPut("/api/codes/{id:int}", UpdateCode)
            .WithName("UpdateCode")
            .WithSummary("Saves over a code.");

        routes.MapGet("/api/codes/{list}/csv", ExportCodes)
            .WithName("ExportCodes")
            .WithSummary("One code list, retired codes included, as CSV.");

        routes.MapPost("/api/codes/{list}/csv", ImportCodes)
            .WithName("ImportCodes")
            .WithSummary("Previews, or with confirm=true saves, a CSV file of codes, added or saved over by code.");

        return routes;
    }

    private static async Task<Results<Ok<IReadOnlyList<CodeResponse>>, ProblemHttpResult>> GetCodes(
        CodeQueries codes,
        string list,
        int? conditionId = null,
        bool inactive = false,
        CancellationToken cancellationToken = default)
    {
        if (!IsAList(list))
        {
            return NoSuchList(list);
        }

        return TypedResults.Ok(await codes.List(list, conditionId, inactive, cancellationToken));
    }

    private static async Task<Results<ContentHttpResult, ProblemHttpResult>> ExportCodes(
        CodeQueries codes,
        string list,
        CancellationToken cancellationToken = default) =>
        IsAList(list)
            ? TypedResults.Text(await codes.Export(list, cancellationToken), "text/csv; charset=utf-8")
            : NoSuchList(list);

    private static async Task<Results<Ok<ReferenceImportResponse>, ProblemHttpResult>> ImportCodes(
        CodeQueries codes,
        string list,
        HttpRequest request,
        bool confirm = false,
        CancellationToken cancellationToken = default)
    {
        if (!IsAList(list))
        {
            return NoSuchList(list);
        }

        using var reader = new StreamReader(request.Body);
        var csv = await reader.ReadToEndAsync(cancellationToken);

        return TypedResults.Ok(await codes.Import(list, csv, confirm, cancellationToken));
    }

    private static async Task<Results<Created<CodeResponse>, ProblemHttpResult>> CreateCode(
        CodeQueries codes,
        string list,
        SaveCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await WhyNotSaved(codes, list, null, request, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        var created = await codes.Add(list, request, cancellationToken);

        return TypedResults.Created($"/api/codes/{created.Id}", created);
    }

    private static async Task<Results<Ok<CodeResponse>, ProblemHttpResult>> UpdateCode(
        CodeQueries codes,
        int id,
        SaveCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await codes.ListOf(id, cancellationToken) is not { } list)
        {
            return TypedResults.Problem(
                detail: $"No code has id {id}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such code.");
        }

        if (await WhyNotSaved(codes, list, id, request, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        return TypedResults.Ok(await codes.Update(id, request, cancellationToken));
    }

    private static async Task<ProblemHttpResult?> WhyNotSaved(
        CodeQueries codes,
        string list,
        int? id,
        SaveCodeRequest request,
        CancellationToken cancellationToken)
    {
        if (WhyNotACode(list, request) is { } why)
        {
            return Refused(why);
        }

        if (await codes.Taken(list, request.Code!.Trim(), id, cancellationToken))
        {
            return TypedResults.Problem(
                detail: $"The code '{request.Code.Trim()}' is already on the {list} list.",
                statusCode: StatusCodes.Status409Conflict,
                title: "The code is already on the list.");
        }

        if (request.ConditionIds is { Count: > 0 } conditionIds
            && await codes.MissingCondition(conditionIds, cancellationToken) is { } missing)
        {
            return Refused($"No condition has id {missing}. GET /api/conditions lists them.");
        }

        return null;
    }

    private static bool IsAList(string list) => CodeLists.All.Contains(list, StringComparer.Ordinal);

    private static string NotAList(string list) =>
        $"'{list}' is not a code list. The lists are {string.Join(", ", CodeLists.All)}.";

    private static ProblemHttpResult NoSuchList(string list) =>
        TypedResults.Problem(
            detail: NotAList(list),
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such code list.");

    private static string? TooLong(string field, string? value, int length) =>
        value?.Trim().Length > length ? $"{field} is longer than {length} characters." : null;

    private static ProblemHttpResult Refused(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The code asked for is not one this endpoint can save.");
}

/// <summary>
/// The code list queries, and the only thing in this tier that holds the
/// <c>DbContext</c> for the code lists.
/// </summary>
internal sealed class CodeQueries(SurvsysDbContext database)
{
    /// <summary>
    /// One list, active codes only unless asked otherwise, ordered by sort
    /// order, then by what a picker shows, then by id so the order is total;
    /// test types narrowed to a condition's where it has any mapped.
    /// </summary>
    public async Task<IReadOnlyList<CodeResponse>> List(
        string list,
        int? conditionId,
        bool inactive,
        CancellationToken cancellationToken)
    {
        var codes = database.CodeValues.Where(code => code.List == list);

        if (!inactive)
        {
            codes = codes.Where(code => code.Active);
        }

        if (list == CodeLists.TestType && conditionId is { } condition)
        {
            var mapped = database.CodeValueConditions.Where(link => link.ConditionId == condition);

            if (await mapped.AnyAsync(cancellationToken))
            {
                codes = codes.Where(code => mapped.Any(link => link.CodeValueId == code.Id));
            }
        }

        return await Responses(database, Ordered(codes)).ToListAsync(cancellationToken);
    }

    /// <summary>Codes in a list's order: by sort order, then by what a picker shows, then by id.</summary>
    public static IQueryable<CodeValue> Ordered(IQueryable<CodeValue> codes) =>
        codes
            .OrderBy(code => code.SortOrder)
            .ThenBy(code => code.Description ?? code.Code)
            .ThenBy(code => code.Id);

    /// <summary>Codes as answered, each with its conditions.</summary>
    public static IQueryable<CodeResponse> Responses(SurvsysDbContext database, IQueryable<CodeValue> codes) =>
        codes.Select(code => new CodeResponse(
            code.Id,
            code.List,
            code.Code,
            code.Description,
            code.SortOrder,
            code.Active,
            database.CodeValueConditions
                .Where(link => link.CodeValueId == code.Id)
                .Select(link => link.ConditionId)
                .ToList(),
            code.State,
            code.Fips,
            code.Kind));

    public Task<string> Export(string list, CancellationToken cancellationToken) =>
        ReferenceImport.Export(database, new CodeListTable(list), cancellationToken);

    /// <summary>What importing a CSV file onto a list would do, written only where <paramref name="confirm"/> is true.</summary>
    public Task<ReferenceImportResponse> Import(
        string list, string? csv, bool confirm, CancellationToken cancellationToken) =>
        ReferenceImport.Import(database, new CodeListTable(list), csv, confirm, cancellationToken);

    /// <summary>The list the code with that id is on, or null where no code has it.</summary>
    public Task<string?> ListOf(int id, CancellationToken cancellationToken) =>
        database.CodeValues
            .Where(code => code.Id == id)
            .Select(code => code.List)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Whether another code on the list already has this code, compared as
    /// the database compares it.
    /// </summary>
    public Task<bool> Taken(string list, string code, int? exceptId, CancellationToken cancellationToken) =>
        database.CodeValues.AnyAsync(
            existing => existing.List == list && existing.Code == code && existing.Id != exceptId,
            cancellationToken);

    /// <summary>The first of these condition ids no condition has, or null where every one exists.</summary>
    public async Task<int?> MissingCondition(IReadOnlyList<int> conditionIds, CancellationToken cancellationToken)
    {
        var found = await database.Conditions
            .Where(condition => conditionIds.Contains(condition.Id))
            .Select(condition => condition.Id)
            .ToListAsync(cancellationToken);

        return conditionIds.Where(id => !found.Contains(id)).Select(id => (int?)id).FirstOrDefault();
    }

    public async Task<CodeResponse> Add(string list, SaveCodeRequest request, CancellationToken cancellationToken)
    {
        var code = new CodeValue { List = list, Code = request.Code!.Trim() };
        Apply(code, request);
        database.CodeValues.Add(code);
        await database.SaveChangesAsync(cancellationToken);

        return await Link(code, request, cancellationToken);
    }

    /// <summary>Saves over the code, which the caller has found exists.</summary>
    public async Task<CodeResponse> Update(int id, SaveCodeRequest request, CancellationToken cancellationToken)
    {
        var code = await database.CodeValues.SingleAsync(code => code.Id == id, cancellationToken);
        Apply(code, request);

        await database.CodeValueConditions
            .Where(link => link.CodeValueId == id)
            .ExecuteDeleteAsync(cancellationToken);

        return await Link(code, request, cancellationToken);
    }

    /// <summary>
    /// The request's conditions onto the saved code, and the code as answered.
    /// </summary>
    private async Task<CodeResponse> Link(CodeValue code, SaveCodeRequest request, CancellationToken cancellationToken)
    {
        var conditionIds = (request.ConditionIds ?? []).Distinct().ToList();

        database.CodeValueConditions.AddRange(conditionIds.Select(conditionId =>
            new CodeValueCondition { CodeValueId = code.Id, ConditionId = conditionId }));
        await database.SaveChangesAsync(cancellationToken);

        return new CodeResponse(
            code.Id, code.List, code.Code, code.Description, code.SortOrder, code.Active, conditionIds,
            code.State, code.Fips, code.Kind);
    }

    /// <summary>
    /// The request's values onto the code: trimmed, a blank description stored
    /// as null.
    /// </summary>
    private static void Apply(CodeValue code, SaveCodeRequest request)
    {
        code.Code = request.Code!.Trim();
        code.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        code.SortOrder = request.SortOrder;
        code.Active = request.Active;
    }
}

/// <summary>
/// One code list as a reference table: keyed by the code within the list, the
/// county list carrying state, FIPS code and kind and the test types their
/// conditions, which are replaced whole once the code is saved.
/// </summary>
internal sealed class CodeListTable(string list) : ReferenceTable<CodeValue, CodeResponse>
{
    public override IReadOnlyList<string> Columns => CodeEndpoints.CsvColumns(list);

    public override Task<List<CodeResponse>> List(SurvsysDbContext database, CancellationToken cancellationToken) =>
        CodeQueries.Responses(database, CodeQueries.Ordered(database.CodeValues.Where(code => code.List == list)))
            .ToListAsync(cancellationToken);

    public override IReadOnlyList<string?> Fields(CodeResponse row) => CodeEndpoints.CsvFields(list, row);

    public override (CodeResponse? Row, string? Why) Parse(CsvRow row) => CodeEndpoints.FromCsv(list, row);

    public override string? WhyNot(CodeResponse row) => CodeEndpoints.WhyNotACsvCode(list, row);

    public override string KeyOf(CodeResponse row) => row.Code.Trim();

    public override Task<CodeValue?> ByKey(
        SurvsysDbContext database, CodeResponse row, CancellationToken cancellationToken) =>
        database.CodeValues.SingleOrDefaultAsync(
            code => code.List == list && code.Code == row.Code.Trim(), cancellationToken);

    public override Task<CodeResponse> RowOf(
        SurvsysDbContext database, CodeValue entity, CancellationToken cancellationToken) =>
        CodeQueries.Responses(database, database.CodeValues.Where(code => code.Id == entity.Id))
            .SingleAsync(cancellationToken);

    public override CodeValue New(CodeResponse row) => new() { List = list, Code = row.Code.Trim() };

    public override async Task<string?> Apply(
        SurvsysDbContext database, CodeValue entity, CodeResponse row, CancellationToken cancellationToken)
    {
        var conditionIds = row.ConditionIds.ToList();
        var found = await database.Conditions
            .Where(condition => conditionIds.Contains(condition.Id))
            .Select(condition => condition.Id)
            .ToListAsync(cancellationToken);

        if (conditionIds.Except(found).Select(id => (int?)id).FirstOrDefault() is { } missing)
        {
            return $"condition_ids names {missing}, which is not a condition GET /api/conditions lists.";
        }

        entity.Code = row.Code.Trim();
        entity.Description = Blank(row.Description);
        entity.SortOrder = row.SortOrder;
        entity.Active = row.Active;

        if (list == CodeLists.County)
        {
            entity.State = Blank(row.State);
            entity.Fips = Blank(row.Fips);
            entity.Kind = Blank(row.Kind);
        }

        return null;
    }

    public override async Task Saved(
        SurvsysDbContext database, CodeValue entity, CodeResponse row, CancellationToken cancellationToken)
    {
        if (list != CodeLists.TestType)
        {
            return;
        }

        await database.CodeValueConditions
            .Where(link => link.CodeValueId == entity.Id)
            .ExecuteDeleteAsync(cancellationToken);
        database.CodeValueConditions.AddRange(row.ConditionIds.Distinct().Select(conditionId =>
            new CodeValueCondition { CodeValueId = entity.Id, ConditionId = conditionId }));
        await database.SaveChangesAsync(cancellationToken);
    }
}
