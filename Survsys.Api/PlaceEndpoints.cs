using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>/api/ct-towns</c> and <c>/api/zip-codes</c>: the Connecticut towns with
/// the county and planning region each lies in, and the zip codes with the
/// county each lies in — what fills an address's county values when it is
/// saved without them. Each is listed, added to, saved over, exported as CSV
/// and imported from it.
/// </summary>
/// <remarks>
/// Both tables are kept current by an administrator rather than by a release,
/// so a row corrected here is the one the next address save reads. A town is
/// keyed by its name and a zip code by its five digits; a key is never
/// changed, so a new name is a new row, and no row is deleted. Every county
/// and planning region named is a code on the <c>county</c> list. A CSV
/// import is <see cref="ReferenceImport"/>'s, previewed and then confirmed.
/// The tables are small, so each is listed whole. The handlers are handed
/// <see cref="PlaceQueries"/> and not the <c>DbContext</c>, because the
/// architecture test lets no <c>Survsys.Data</c> type into an endpoint
/// signature.
/// </remarks>
public static class PlaceEndpoints
{
    public const string TownsPath = "/api/ct-towns";

    public const string ZipCodesPath = "/api/zip-codes";

    public static readonly IReadOnlyList<string> TownColumns = ["town", "county", "planning_region"];

    public static readonly IReadOnlyList<string> ZipCodeColumns = ["zip_code", "county"];

    private const int TextLength = 50;

    /// <summary>Why this is not a town this tier will save, as far as can be said without the database, or null.</summary>
    public static string? WhyNotATown(CtTownRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return Required("town", row.Town) ?? Required("county", row.County) ?? Required("planningRegion", row.PlanningRegion);
    }

    /// <summary>Why this is not a zip code this tier will save, as far as can be said without the database, or null.</summary>
    public static string? WhyNotAZipCode(ZipCodeRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return ZipAgencyRules.Normalize(row.Code) is null
            ? $"'{row.Code}' is not a zip code. A zip code is five digits, or a ZIP+4 such as 84101-1234."
            : TooLong("county", row.County);
    }

    public static IReadOnlyList<string?> Fields(CtTownRow row) => [row.Town, row.County, row.PlanningRegion];

    public static IReadOnlyList<string?> Fields(ZipCodeRow row) => [row.Code, row.County];

    /// <summary>A town read from its CSV row.</summary>
    public static CtTownRow Town(CsvRow row) =>
        new(row.Value("town") ?? "", row.Value("county") ?? "", row.Value("planning_region") ?? "");

    /// <summary>A zip code read from its CSV row, a ZIP+4 read as its five digits.</summary>
    public static ZipCodeRow ZipCode(CsvRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var code = row.Value("zip_code") ?? "";
        return new(ZipAgencyRules.Normalize(code) ?? code, row.Value("county"));
    }

    /// <summary>Registers what the place endpoints need of the container.</summary>
    public static IServiceCollection AddPlaces(this IServiceCollection services) =>
        services.AddScoped<PlaceQueries>();

    public static IEndpointRouteBuilder MapPlaces(this IEndpointRouteBuilder routes)
    {
        MapTable(routes, TownsPath, PlaceQueries.Towns, "Connecticut town", key => key.Trim());
        MapTable(routes, ZipCodesPath, PlaceQueries.ZipCodes, "zip code", ZipAgencyRules.Normalize);

        return routes;
    }

    /// <summary>
    /// One table's list, add, save over, export and import; <paramref name="routeKey"/>
    /// reads the key in a save's path as the table keys a row.
    /// </summary>
    private static void MapTable<TEntity, TRow>(
        IEndpointRouteBuilder routes,
        string path,
        ReferenceTable<TEntity, TRow> table,
        string noun,
        Func<string, string?> routeKey)
        where TEntity : class
        where TRow : class
    {
        var name = path["/api/".Length..];

        routes.MapGet(path, (PlaceQueries places, CancellationToken cancellationToken) =>
                places.List(table, cancellationToken))
            .WithName($"List-{name}")
            .WithSummary($"Every {noun}.");

        routes.MapPost(path, async Task<Results<Created<TRow>, ProblemHttpResult>> (
                PlaceQueries places, TRow row, CancellationToken cancellationToken) =>
            {
                if (table.WhyNot(row) is { } why)
                {
                    return Refused(noun, why);
                }

                if (await places.Exists(table, row, cancellationToken))
                {
                    return TypedResults.Problem(
                        detail: $"{table.KeyOf(row)} is already on file; save over it instead.",
                        statusCode: StatusCodes.Status409Conflict,
                        title: $"The {noun} is already on file.");
                }

                var (saved, unresolved) = await places.Save(table, row, cancellationToken);
                return saved is null
                    ? Refused(noun, unresolved!)
                    : TypedResults.Created($"{path}/{Uri.EscapeDataString(table.KeyOf(saved))}", saved);
            })
            .WithName($"Create-{name}")
            .WithSummary($"Adds a {noun}.");

        routes.MapPut($"{path}/{{key}}", async Task<Results<Ok<TRow>, ProblemHttpResult>> (
                PlaceQueries places, string key, TRow row, CancellationToken cancellationToken) =>
            {
                if (table.WhyNot(row) is { } why)
                {
                    return Refused(noun, why);
                }

                if (!string.Equals(routeKey(key), table.KeyOf(row), StringComparison.OrdinalIgnoreCase))
                {
                    return Refused(noun, $"the {noun} is {key} in the path and {table.KeyOf(row)} in the body: "
                        + "a key is not changed, so a new one is added as a new row.");
                }

                if (!await places.Exists(table, row, cancellationToken))
                {
                    return TypedResults.Problem(
                        detail: $"No {noun} is {key}.",
                        statusCode: StatusCodes.Status404NotFound,
                        title: $"There is no such {noun}.");
                }

                var (saved, unresolved) = await places.Save(table, row, cancellationToken);
                return saved is null ? Refused(noun, unresolved!) : TypedResults.Ok(saved);
            })
            .WithName($"Update-{name}")
            .WithSummary($"Saves over a {noun}.");

        routes.MapGet($"{path}/csv", async (PlaceQueries places, CancellationToken cancellationToken) =>
                TypedResults.Text(await places.Export(table, cancellationToken), "text/csv; charset=utf-8"))
            .WithName($"Export-{name}")
            .WithSummary($"Every {noun}, as CSV.");

        routes.MapPost($"{path}/csv", async (
                PlaceQueries places,
                HttpRequest request,
                CancellationToken cancellationToken,
                bool confirm = false) =>
            {
                using var reader = new StreamReader(request.Body);
                var csv = await reader.ReadToEndAsync(cancellationToken);
                return TypedResults.Ok(await places.Import(table, csv, confirm, cancellationToken));
            })
            .WithName($"Import-{name}")
            .WithSummary($"Previews, or with confirm=true saves, each {noun} of a CSV file, added or saved over by its key.");
    }

    private static string? Required(string field, string? value) =>
        string.IsNullOrWhiteSpace(value) ? $"{field} is required." : TooLong(field, value);

    private static string? TooLong(string field, string? value) =>
        value?.Trim().Length > TextLength ? $"{field} is longer than {TextLength} characters." : null;

    private static ProblemHttpResult Refused(string noun, string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: $"The {noun} asked for is not one this endpoint can save.");
}

/// <summary>
/// The place queries, and the only thing in this tier that holds the
/// <c>DbContext</c> for the towns and the zip codes.
/// </summary>
internal sealed class PlaceQueries(SurvsysDbContext database)
{
    public static readonly ReferenceTable<CtTown, CtTownRow> Towns = new CtTownTable();

    public static readonly ReferenceTable<ZipCode, ZipCodeRow> ZipCodes = new ZipCodeTable();

    public async Task<IReadOnlyList<TRow>> List<TEntity, TRow>(
        ReferenceTable<TEntity, TRow> table, CancellationToken cancellationToken)
        where TEntity : class
        where TRow : class =>
        await table.List(database, cancellationToken);

    public async Task<bool> Exists<TEntity, TRow>(
        ReferenceTable<TEntity, TRow> table, TRow row, CancellationToken cancellationToken)
        where TEntity : class
        where TRow : class =>
        await table.ByKey(database, row, cancellationToken) is not null;

    /// <summary>
    /// Saves the row over the entry with its key, or as a new one, answering
    /// the saved row or why it was not saved.
    /// </summary>
    public async Task<(TRow? Saved, string? Why)> Save<TEntity, TRow>(
        ReferenceTable<TEntity, TRow> table, TRow row, CancellationToken cancellationToken)
        where TEntity : class
        where TRow : class
    {
        var holder = await table.ByKey(database, row, cancellationToken);
        var entity = holder ?? table.New(row);

        if (await table.Apply(database, entity, row, cancellationToken) is { } why)
        {
            return (null, why);
        }

        if (holder is null)
        {
            database.Add(entity);
        }

        await database.SaveChangesAsync(cancellationToken);

        return (await table.RowOf(database, entity, cancellationToken), null);
    }

    public Task<string> Export<TEntity, TRow>(ReferenceTable<TEntity, TRow> table, CancellationToken cancellationToken)
        where TEntity : class
        where TRow : class =>
        ReferenceImport.Export(database, table, cancellationToken);

    /// <summary>What importing a CSV file would do, written only where <paramref name="confirm"/> is true.</summary>
    public Task<ReferenceImportResponse> Import<TEntity, TRow>(
        ReferenceTable<TEntity, TRow> table, string? csv, bool confirm, CancellationToken cancellationToken)
        where TEntity : class
        where TRow : class =>
        ReferenceImport.Import(database, table, csv, confirm, cancellationToken);

    /// <summary>Why a code is not on the county list, or null where it is or none is given.</summary>
    internal static async Task<string?> NotACounty(
        SurvsysDbContext database, string field, string? code, CancellationToken cancellationToken) =>
        code is null || await database.CodeValues.AnyAsync(
            value => value.List == CodeLists.County && value.Code == code, cancellationToken)
            ? null
            : $"{field} {code} is not a code on the county list GET /api/codes/county lists.";
}

/// <summary>The Connecticut towns, keyed by the town's name.</summary>
internal sealed class CtTownTable : ReferenceTable<CtTown, CtTownRow>
{
    public override IReadOnlyList<string> Columns => PlaceEndpoints.TownColumns;

    public override async Task<List<CtTownRow>> List(SurvsysDbContext database, CancellationToken cancellationToken) =>
        (await database.CtTowns
            .Select(town => new CtTownRow(town.Town, town.County, town.PlanningRegion))
            .ToListAsync(cancellationToken))
        .OrderBy(row => row.Town, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public override IReadOnlyList<string?> Fields(CtTownRow row) => PlaceEndpoints.Fields(row);

    public override (CtTownRow? Row, string? Why) Parse(CsvRow row) => (PlaceEndpoints.Town(row), null);

    public override string? WhyNot(CtTownRow row) => PlaceEndpoints.WhyNotATown(row);

    public override string KeyOf(CtTownRow row) => row.Town.Trim();

    public override Task<CtTown?> ByKey(SurvsysDbContext database, CtTownRow row, CancellationToken cancellationToken) =>
        database.CtTowns.SingleOrDefaultAsync(town => town.Town == row.Town.Trim(), cancellationToken);

    public override Task<CtTownRow> RowOf(SurvsysDbContext database, CtTown entity, CancellationToken cancellationToken) =>
        Task.FromResult(new CtTownRow(entity.Town, entity.County, entity.PlanningRegion));

    public override CtTown New(CtTownRow row) =>
        new() { Town = row.Town.Trim(), County = row.County.Trim(), PlanningRegion = row.PlanningRegion.Trim() };

    public override async Task<string?> Apply(
        SurvsysDbContext database, CtTown entity, CtTownRow row, CancellationToken cancellationToken)
    {
        var county = row.County.Trim();
        var region = row.PlanningRegion.Trim();

        if ((await PlaceQueries.NotACounty(database, "county", county, cancellationToken)
            ?? await PlaceQueries.NotACounty(database, "planningRegion", region, cancellationToken)) is { } why)
        {
            return why;
        }

        entity.County = county;
        entity.PlanningRegion = region;
        return null;
    }
}

/// <summary>The zip codes, keyed by their five digits.</summary>
internal sealed class ZipCodeTable : ReferenceTable<ZipCode, ZipCodeRow>
{
    public override IReadOnlyList<string> Columns => PlaceEndpoints.ZipCodeColumns;

    public override async Task<List<ZipCodeRow>> List(SurvsysDbContext database, CancellationToken cancellationToken) =>
        (await database.ZipCodes
            .Select(zip => new ZipCodeRow(zip.Code, zip.County))
            .ToListAsync(cancellationToken))
        .OrderBy(row => row.Code, StringComparer.Ordinal)
        .ToList();

    public override IReadOnlyList<string?> Fields(ZipCodeRow row) => PlaceEndpoints.Fields(row);

    public override (ZipCodeRow? Row, string? Why) Parse(CsvRow row) => (PlaceEndpoints.ZipCode(row), null);

    public override string? WhyNot(ZipCodeRow row) => PlaceEndpoints.WhyNotAZipCode(row);

    public override string KeyOf(ZipCodeRow row) => ZipAgencyRules.Normalize(row.Code) ?? row.Code.Trim();

    public override Task<ZipCode?> ByKey(SurvsysDbContext database, ZipCodeRow row, CancellationToken cancellationToken)
    {
        var code = KeyOf(row);
        return database.ZipCodes.SingleOrDefaultAsync(zip => zip.Code == code, cancellationToken);
    }

    public override Task<ZipCodeRow> RowOf(SurvsysDbContext database, ZipCode entity, CancellationToken cancellationToken) =>
        Task.FromResult(new ZipCodeRow(entity.Code, entity.County));

    public override ZipCode New(ZipCodeRow row) => new() { Code = KeyOf(row) };

    public override async Task<string?> Apply(
        SurvsysDbContext database, ZipCode entity, ZipCodeRow row, CancellationToken cancellationToken)
    {
        var county = Blank(row.County);

        if (await PlaceQueries.NotACounty(database, "county", county, cancellationToken) is { } why)
        {
            return why;
        }

        entity.County = county;
        return null;
    }
}
