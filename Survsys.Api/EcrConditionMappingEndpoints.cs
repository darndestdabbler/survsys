using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>/api/ecr-condition-mappings</c>: the condition codes a reportability
/// response can carry, RCTC condition groupers and SNOMED condition codes, and
/// the catalogue condition each maps to.
/// </summary>
/// <remarks>
/// Reference data an administrator keeps current: listed, added to, saved
/// over, deleted, exported as CSV and imported from it, keyed by code system
/// and code. An RCTC release's condition list comes in through the import,
/// with its condition column blank, and each code is then mapped here. In the
/// CSV a condition is named rather than numbered, so a release's list can be
/// mapped in a spreadsheet; on the wire it is chosen by id. As in
/// <see cref="VaccineEndpoints"/>, the handlers are handed
/// <see cref="EcrConditionMappingQueries"/> and not the <c>DbContext</c>.
/// </remarks>
public static class EcrConditionMappingEndpoints
{
    private const int CodeSystemLength = 100;

    private const int CodeLength = 50;

    private const int DisplayNameLength = 500;

    /// <summary>The mapping's CSV columns, in the order an export writes them.</summary>
    public static readonly IReadOnlyList<string> CsvColumns = ["code_system", "code", "display_name", "condition"];

    /// <summary>
    /// Why this is not a mapping this tier will save, as far as can be said
    /// without the database, or null when it is one.
    /// </summary>
    public static string? WhyNotAMapping(SaveEcrConditionMappingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return WhyNotAMapping(request.CodeSystem, request.Code, request.DisplayName);
    }

    /// <summary>The same, for a row of the CSV file.</summary>
    public static string? WhyNotAMapping(EcrConditionMappingCsvRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return WhyNotAMapping(row.CodeSystem, row.Code, row.DisplayName);
    }

    /// <summary>A mapping as its CSV fields, in <see cref="CsvColumns"/> order.</summary>
    public static IReadOnlyList<string?> CsvFields(EcrConditionMappingCsvRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return [row.CodeSystem, row.Code, row.DisplayName, row.Condition];
    }

    /// <summary>A mapping read from its CSV row; a blank display name or condition is none.</summary>
    public static EcrConditionMappingCsvRow FromCsv(CsvRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return new EcrConditionMappingCsvRow(
            row.Value("code_system")?.Trim() ?? "",
            row.Value("code")?.Trim() ?? "",
            Blank(row.Value("display_name")),
            Blank(row.Value("condition")));
    }

    /// <summary>Registers what the mapping endpoints need of the container.</summary>
    public static IServiceCollection AddEcrConditionMappings(this IServiceCollection services) =>
        services.AddScoped<EcrConditionMappingQueries>();

    public static IEndpointRouteBuilder MapEcrConditionMappings(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/ecr-condition-mappings", GetMappings)
            .WithName("GetEcrConditionMappings")
            .WithSummary("The eCR condition codes, each with the catalogue condition it maps to.");

        routes.MapPost("/api/ecr-condition-mappings", CreateMapping)
            .WithName("CreateEcrConditionMapping")
            .WithSummary("Adds a condition code to the mapping.");

        routes.MapPut("/api/ecr-condition-mappings/{id:int}", UpdateMapping)
            .WithName("UpdateEcrConditionMapping")
            .WithSummary("Saves over a condition code in the mapping.");

        routes.MapDelete("/api/ecr-condition-mappings/{id:int}", DeleteMapping)
            .WithName("DeleteEcrConditionMapping")
            .WithSummary("Deletes a condition code from the mapping.");

        routes.MapGet(
                "/api/ecr-condition-mappings/csv",
                async (EcrConditionMappingQueries mappings, CancellationToken cancellationToken) =>
                    TypedResults.Text(await mappings.Export(cancellationToken), "text/csv; charset=utf-8"))
            .WithName("ExportEcrConditionMappings")
            .WithSummary("The eCR condition mapping, as CSV.");

        routes.MapPost("/api/ecr-condition-mappings/csv", async (
                EcrConditionMappingQueries mappings,
                HttpRequest request,
                CancellationToken cancellationToken,
                bool confirm = false) =>
            {
                using var reader = new StreamReader(request.Body);
                var csv = await reader.ReadToEndAsync(cancellationToken);
                return TypedResults.Ok(await mappings.Import(csv, confirm, cancellationToken));
            })
            .WithName("ImportEcrConditionMappings")
            .WithSummary("Previews, or with confirm=true saves, each condition code of a CSV file, such as an RCTC "
                + "release's condition list, added or saved over by its code system and code.");

        return routes;
    }

    private static async Task<Ok<IReadOnlyList<EcrConditionMappingResponse>>> GetMappings(
        EcrConditionMappingQueries mappings,
        CancellationToken cancellationToken = default) =>
        TypedResults.Ok(await mappings.List(cancellationToken));

    private static async Task<Results<Created<EcrConditionMappingResponse>, ProblemHttpResult>> CreateMapping(
        EcrConditionMappingQueries mappings,
        SaveEcrConditionMappingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await WhyNot(mappings, null, request, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        var created = await mappings.Save(null, request, cancellationToken);
        return TypedResults.Created($"/api/ecr-condition-mappings/{created!.Id}", created);
    }

    private static async Task<Results<Ok<EcrConditionMappingResponse>, ProblemHttpResult>> UpdateMapping(
        EcrConditionMappingQueries mappings,
        int id,
        SaveEcrConditionMappingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await WhyNot(mappings, id, request, cancellationToken) is { } refusal)
        {
            return refusal;
        }

        return await mappings.Save(id, request, cancellationToken) is { } saved
            ? TypedResults.Ok(saved)
            : NoSuchMapping(id);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteMapping(
        EcrConditionMappingQueries mappings,
        int id,
        CancellationToken cancellationToken = default) =>
        await mappings.Delete(id, cancellationToken)
            ? TypedResults.NoContent()
            : NoSuchMapping(id);

    /// <summary>The refusal of a mapping to save over <paramref name="id"/>, or to add where it is null; null to save it.</summary>
    private static async Task<ProblemHttpResult?> WhyNot(
        EcrConditionMappingQueries mappings,
        int? id,
        SaveEcrConditionMappingRequest request,
        CancellationToken cancellationToken)
    {
        if (WhyNotAMapping(request) is { } why)
        {
            return Refused(why);
        }

        if (request.ConditionId is { } conditionId && !await mappings.ConditionExists(conditionId, cancellationToken))
        {
            return Refused($"conditionId {conditionId} is not a condition on file.");
        }

        if (await mappings.CodeHolder(request.CodeSystem, request.Code, cancellationToken) is { } holder && holder != id)
        {
            return TypedResults.Problem(
                detail: $"Code {request.Code.Trim()} of code system {request.CodeSystem.Trim()} is already mapped; "
                    + "save over that one instead.",
                statusCode: StatusCodes.Status409Conflict,
                title: "The condition code is already on file.");
        }

        return null;
    }

    private static string? WhyNotAMapping(string? codeSystem, string? code, string? displayName)
    {
        if (string.IsNullOrWhiteSpace(codeSystem))
        {
            return "codeSystem is required: a code is read within its code system.";
        }

        if (codeSystem.Trim().Length > CodeSystemLength)
        {
            return $"codeSystem is longer than {CodeSystemLength} characters.";
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return "code is required.";
        }

        if (code.Trim().Length > CodeLength)
        {
            return $"code is longer than {CodeLength} characters.";
        }

        return displayName?.Trim().Length > DisplayNameLength
            ? $"displayName is longer than {DisplayNameLength} characters."
            : null;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ProblemHttpResult NoSuchMapping(int id) =>
        TypedResults.Problem(
            detail: $"No eCR condition mapping has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such condition mapping.");

    private static ProblemHttpResult Refused(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The condition mapping asked for is not one this endpoint can save.");
}

/// <summary>
/// One row of the eCR condition mapping's CSV file: a code, and the catalogue
/// condition it maps to by name, or null where it is unmapped.
/// </summary>
public sealed record EcrConditionMappingCsvRow(string CodeSystem, string Code, string? DisplayName, string? Condition);

/// <summary>The mapping queries, and the only thing in this tier that holds the <c>DbContext</c> for them.</summary>
internal sealed class EcrConditionMappingQueries(SurvsysDbContext database)
{
    public static readonly ReferenceTable<EcrConditionMapping, EcrConditionMappingCsvRow> Table =
        new EcrConditionMappingTable();

    /// <summary>Every mapping with its condition's name, by code system and then code.</summary>
    public async Task<IReadOnlyList<EcrConditionMappingResponse>> List(CancellationToken cancellationToken) =>
        await (
            from mapping in database.EcrConditionMappings
            join condition in database.Conditions on mapping.ConditionId equals condition.Id into mapped
            from condition in mapped.DefaultIfEmpty()
            orderby mapping.CodeSystem, mapping.Code
            select new EcrConditionMappingResponse(
                mapping.Id,
                mapping.CodeSystem,
                mapping.Code,
                mapping.DisplayName,
                mapping.ConditionId,
                condition == null ? null : condition.Name))
            .ToListAsync(cancellationToken);

    /// <summary>The id of the mapping that holds the code, or null where none does.</summary>
    public async Task<int?> CodeHolder(string codeSystem, string code, CancellationToken cancellationToken) =>
        await database.EcrConditionMappings
            .Where(mapping => mapping.CodeSystem == codeSystem.Trim() && mapping.Code == code.Trim())
            .Select(mapping => (int?)mapping.Id)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>Whether the catalogue holds a condition with that id.</summary>
    public Task<bool> ConditionExists(int id, CancellationToken cancellationToken) =>
        database.Conditions.AnyAsync(condition => condition.Id == id, cancellationToken);

    /// <summary>
    /// Saves the mapping over the one with the id, or as a new one where the
    /// id is null, and answers it; null where no mapping has the id. The caller
    /// has proven the request one to save.
    /// </summary>
    public async Task<EcrConditionMappingResponse?> Save(
        int? id,
        SaveEcrConditionMappingRequest request,
        CancellationToken cancellationToken)
    {
        var mapping = id is null
            ? new EcrConditionMapping()
            : await database.EcrConditionMappings.SingleOrDefaultAsync(mapping => mapping.Id == id, cancellationToken);

        if (mapping is null)
        {
            return null;
        }

        mapping.CodeSystem = request.CodeSystem.Trim();
        mapping.Code = request.Code.Trim();
        mapping.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim();
        mapping.ConditionId = request.ConditionId;

        if (id is null)
        {
            database.EcrConditionMappings.Add(mapping);
        }

        await database.SaveChangesAsync(cancellationToken);

        var conditionName = mapping.ConditionId is null
            ? null
            : await database.Conditions
                .Where(condition => condition.Id == mapping.ConditionId)
                .Select(condition => condition.Name)
                .SingleAsync(cancellationToken);

        return new EcrConditionMappingResponse(
            mapping.Id, mapping.CodeSystem, mapping.Code, mapping.DisplayName, mapping.ConditionId, conditionName);
    }

    /// <summary>Deletes the mapping with the id, answering whether there was one.</summary>
    public async Task<bool> Delete(int id, CancellationToken cancellationToken) =>
        await database.EcrConditionMappings
            .Where(mapping => mapping.Id == id)
            .ExecuteDeleteAsync(cancellationToken) > 0;

    public Task<string> Export(CancellationToken cancellationToken) =>
        ReferenceImport.Export(database, Table, cancellationToken);

    /// <summary>What importing a CSV file would do, written only where <paramref name="confirm"/> is true.</summary>
    public Task<ReferenceImportResponse> Import(string? csv, bool confirm, CancellationToken cancellationToken) =>
        ReferenceImport.Import(database, Table, csv, confirm, cancellationToken);
}

/// <summary>
/// The eCR condition mapping, keyed by code system and code. A row's condition
/// is the catalogue condition of that name, and a name no condition has, or
/// more than one has, is refused.
/// </summary>
internal sealed class EcrConditionMappingTable : ReferenceTable<EcrConditionMapping, EcrConditionMappingCsvRow>
{
    public override IReadOnlyList<string> Columns => EcrConditionMappingEndpoints.CsvColumns;

    public override async Task<List<EcrConditionMappingCsvRow>> List(
        SurvsysDbContext database, CancellationToken cancellationToken) =>
        await (
            from mapping in database.EcrConditionMappings
            join condition in database.Conditions on mapping.ConditionId equals condition.Id into mapped
            from condition in mapped.DefaultIfEmpty()
            orderby mapping.CodeSystem, mapping.Code
            select new EcrConditionMappingCsvRow(
                mapping.CodeSystem, mapping.Code, mapping.DisplayName, condition == null ? null : condition.Name))
            .ToListAsync(cancellationToken);

    public override IReadOnlyList<string?> Fields(EcrConditionMappingCsvRow row) =>
        EcrConditionMappingEndpoints.CsvFields(row);

    public override (EcrConditionMappingCsvRow? Row, string? Why) Parse(CsvRow row) =>
        (EcrConditionMappingEndpoints.FromCsv(row), null);

    public override string? WhyNot(EcrConditionMappingCsvRow row) => EcrConditionMappingEndpoints.WhyNotAMapping(row);

    public override string KeyOf(EcrConditionMappingCsvRow row) => $"{row.CodeSystem}|{row.Code}";

    public override Task<EcrConditionMapping?> ByKey(
        SurvsysDbContext database, EcrConditionMappingCsvRow row, CancellationToken cancellationToken) =>
        database.EcrConditionMappings.SingleOrDefaultAsync(
            mapping => mapping.CodeSystem == row.CodeSystem && mapping.Code == row.Code, cancellationToken);

    public override async Task<EcrConditionMappingCsvRow> RowOf(
        SurvsysDbContext database, EcrConditionMapping entity, CancellationToken cancellationToken) =>
        new(
            entity.CodeSystem,
            entity.Code,
            entity.DisplayName,
            entity.ConditionId is null
                ? null
                : await database.Conditions
                    .Where(condition => condition.Id == entity.ConditionId)
                    .Select(condition => condition.Name)
                    .SingleAsync(cancellationToken));

    public override EcrConditionMapping New(EcrConditionMappingCsvRow row) =>
        new() { CodeSystem = row.CodeSystem, Code = row.Code };

    public override async Task<string?> Apply(
        SurvsysDbContext database,
        EcrConditionMapping entity,
        EcrConditionMappingCsvRow row,
        CancellationToken cancellationToken)
    {
        int? conditionId = null;
        if (row.Condition is { } name)
        {
            var named = await database.Conditions
                .Where(condition => condition.Name == name)
                .Select(condition => condition.Id)
                .Take(2)
                .ToListAsync(cancellationToken);

            if (named.Count != 1)
            {
                return named.Count == 0
                    ? $"condition '{name}' is not a condition on file."
                    : $"condition '{name}' names more than one condition on file.";
            }

            conditionId = named[0];
        }

        entity.CodeSystem = row.CodeSystem;
        entity.Code = row.Code;
        entity.DisplayName = row.DisplayName;
        entity.ConditionId = conditionId;
        return null;
    }
}
