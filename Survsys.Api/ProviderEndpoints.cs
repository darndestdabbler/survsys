using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>/api/providers</c>: the provider registry, one provider, and their
/// administration — create, update, and CSV export and import.
/// </summary>
/// <remarks>
/// The handlers are handed <see cref="ProviderQueries"/> and not the
/// <c>DbContext</c>, because the architecture test lets no
/// <c>Survsys.Data</c> type into an endpoint signature. A provider type that
/// is not a code on the <c>provider_type</c> list is a 400, asked before the
/// write rather than left to the foreign key to refuse as a 500.
/// </remarks>
public static class ProviderEndpoints
{
    private const int NameLength = 100;

    private const int PhoneLength = 30;

    private const int EmailLength = 200;

    /// <summary>
    /// The projection from the stored provider to the wire shape — every
    /// column. Public because the unit suite proves it.
    /// </summary>
    public static readonly Expression<Func<Provider, ProviderResponse>> ToResponse =
        provider => new ProviderResponse(
            provider.Id,
            provider.ProviderTypeId,
            provider.FirstName,
            provider.MiddleName,
            provider.LastName,
            provider.Phone,
            provider.Email,
            provider.Active);

    /// <summary>
    /// <see cref="ToResponse"/> compiled, for a provider just saved and so in
    /// hand rather than in a query.
    /// </summary>
    public static readonly Func<Provider, ProviderResponse> Respond = ToResponse.Compile();

    /// <summary>
    /// Why this is not a provider this tier will save, as far as can be said
    /// without the database, or null when it is one.
    /// </summary>
    /// <remarks>
    /// Whether the provider type is on its list needs a query and is the
    /// endpoint's.
    /// </remarks>
    public static string? WhyNotAProvider(SaveProviderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            return "lastName is required: the registry is searched by it.";
        }

        if (!string.IsNullOrWhiteSpace(request.Email) && !request.Email.Contains('@', StringComparison.Ordinal))
        {
            return $"email '{request.Email}' is not an email address.";
        }

        return TooLong("lastName", request.LastName, NameLength)
            ?? TooLong("firstName", request.FirstName, NameLength)
            ?? TooLong("middleName", request.MiddleName, NameLength)
            ?? TooLong("phone", request.Phone, PhoneLength)
            ?? TooLong("email", request.Email, EmailLength);
    }

    /// <summary>Why a provider type id is refused: it is not a code on the provider type list.</summary>
    public static string NotAProviderType(int providerTypeId) =>
        $"providerTypeId {providerTypeId} is not a provider type. The provider types are the ones "
            + $"GET /api/codes/{CodeLists.ProviderType} lists.";

    /// <summary>
    /// Registers what the provider endpoints need of the container.
    /// </summary>
    public static IServiceCollection AddProviders(this IServiceCollection services) =>
        services.AddScoped<ProviderQueries>();

    public static IEndpointRouteBuilder MapProviders(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/providers", GetProviders)
            .WithName("GetProviders")
            .WithSummary("The providers, paged, ordered by name and searched by the start of the last name.");

        routes.MapGet("/api/providers/{id:int}", GetProvider)
            .WithName("GetProvider")
            .WithSummary("One provider.");

        routes.MapPost("/api/providers", CreateProvider)
            .WithName("CreateProvider")
            .WithSummary("Creates a provider.");

        routes.MapPut("/api/providers/{id:int}", UpdateProvider)
            .WithName("UpdateProvider")
            .WithSummary("Saves over a provider.");

        routes.MapGet("/api/providers/csv", async (ProviderQueries providers, CancellationToken cancellationToken) =>
                TypedResults.Text(await providers.Export(cancellationToken), "text/csv; charset=utf-8"))
            .WithName("ExportProviders")
            .WithSummary("Every provider, as CSV.");

        routes.MapPost("/api/providers/csv", async (
                ProviderQueries providers,
                HttpRequest request,
                CancellationToken cancellationToken,
                bool confirm = false) =>
            {
                using var reader = new StreamReader(request.Body);
                var csv = await reader.ReadToEndAsync(cancellationToken);
                return TypedResults.Ok(await providers.Import(csv, confirm, cancellationToken));
            })
            .WithName("ImportProviders")
            .WithSummary("Previews, or with confirm=true saves, each provider of a CSV file, added where its id "
                + "is blank and saved over by its id otherwise.");

        return routes;
    }

    /// <summary>The providers' CSV columns, in the order an export writes them.</summary>
    public static readonly IReadOnlyList<string> CsvColumns =
        ["id", "provider_type_id", "first_name", "middle_name", "last_name", "phone", "email", "active"];

    /// <summary>A provider as its CSV fields, in <see cref="CsvColumns"/> order; an id of 0 is blank.</summary>
    public static IReadOnlyList<string?> CsvFields(ProviderResponse row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return
        [
            ReferenceCsv.Number(row.Id == 0 ? null : row.Id), ReferenceCsv.Number(row.ProviderTypeId),
            row.FirstName, row.MiddleName, row.LastName, row.Phone, row.Email, ReferenceCsv.YesNo(row.Active),
        ];
    }

    /// <summary>
    /// A provider read from its CSV row, a blank id read as 0 — a provider to
    /// add — and a blank active as active, or why it cannot be read.
    /// </summary>
    public static (ProviderResponse? Row, string? Why) FromCsv(CsvRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (ReferenceCsv.Optional(row, "id", out var id) is { } badId)
        {
            return (null, badId);
        }

        if (ReferenceCsv.Optional(row, "provider_type_id", out var providerTypeId) is { } badType)
        {
            return (null, badType);
        }

        var active = true;

        if (row.Value("active") is not null && ReferenceCsv.Flag(row, "active", out active) is { } badActive)
        {
            return (null, badActive);
        }

        return (new ProviderResponse(
            id ?? 0,
            providerTypeId,
            row.Value("first_name"),
            row.Value("middle_name"),
            row.Value("last_name") ?? "",
            row.Value("phone"),
            row.Value("email"),
            active), null);
    }

    /// <summary>A provider row as the request that saves it.</summary>
    public static SaveProviderRequest Request(ProviderResponse row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return new(row.LastName, row.FirstName, row.MiddleName, row.ProviderTypeId, row.Phone, row.Email, row.Active);
    }

    private static async Task<Results<Ok<PagedResult<ProviderResponse>>, ProblemHttpResult>> GetProviders(
        ProviderQueries providers,
        string? lastName = null,
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

        return TypedResults.Ok(await providers.Page(lastName, page, pageSize, cancellationToken));
    }

    private static async Task<Results<Ok<ProviderResponse>, ProblemHttpResult>> GetProvider(
        ProviderQueries providers,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (await providers.Find(id, cancellationToken) is not { } provider)
        {
            return NoSuchProvider(id);
        }

        return TypedResults.Ok(provider);
    }

    private static async Task<Results<Created<ProviderResponse>, ProblemHttpResult>> CreateProvider(
        ProviderQueries providers,
        SaveProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await WhyNotSaved(request, providers, cancellationToken) is { } why)
        {
            return Refused(why);
        }

        var created = await providers.Add(request, cancellationToken);

        return TypedResults.Created($"/api/providers/{created.Id}", created);
    }

    private static async Task<Results<Ok<ProviderResponse>, ProblemHttpResult>> UpdateProvider(
        ProviderQueries providers,
        int id,
        SaveProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await WhyNotSaved(request, providers, cancellationToken) is { } why)
        {
            return Refused(why);
        }

        if (await providers.Update(id, request, cancellationToken) is not { } provider)
        {
            return NoSuchProvider(id);
        }

        return TypedResults.Ok(provider);
    }

    private static async Task<string?> WhyNotSaved(
        SaveProviderRequest request,
        ProviderQueries providers,
        CancellationToken cancellationToken) =>
        WhyNotAProvider(request)
            ?? (request.ProviderTypeId is { } type && !await providers.IsProviderType(type, cancellationToken)
                ? NotAProviderType(type)
                : null);

    private static string? TooLong(string field, string? value, int length) =>
        value?.Trim().Length > length ? $"{field} is longer than {length} characters." : null;

    private static ProblemHttpResult NoSuchProvider(int id) =>
        TypedResults.Problem(
            detail: $"No provider has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such provider.");

    private static ProblemHttpResult Refused(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The provider asked for is not one this endpoint can save.");
}

/// <summary>
/// The provider queries, and the only thing in this tier that holds the
/// <c>DbContext</c> for providers.
/// </summary>
internal sealed class ProviderQueries(SurvsysDbContext database)
{
    /// <summary>
    /// One page of providers whose last name starts with
    /// <paramref name="lastName"/> (every provider where it is blank), ordered
    /// by last name, first name and then id so the order is total.
    /// </summary>
    public async Task<PagedResult<ProviderResponse>> Page(
        string? lastName,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var providers = database.Providers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(lastName))
        {
            var start = lastName.Trim();
            providers = providers.Where(provider => provider.LastName.StartsWith(start));
        }

        var total = await providers.CountAsync(cancellationToken);

        var rows = await Ordered(providers)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ProviderEndpoints.ToResponse)
            .ToListAsync(cancellationToken);

        return new PagedResult<ProviderResponse>(rows, page, pageSize, total);
    }

    /// <summary>The provider with that id, or null where no provider has it.</summary>
    public Task<ProviderResponse?> Find(int id, CancellationToken cancellationToken) =>
        database.Providers
            .Where(provider => provider.Id == id)
            .Select(ProviderEndpoints.ToResponse)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>Whether a code on the provider type list has that id.</summary>
    public Task<bool> IsProviderType(int id, CancellationToken cancellationToken) =>
        IsProviderType(database, id, cancellationToken);

    public async Task<ProviderResponse> Add(SaveProviderRequest request, CancellationToken cancellationToken)
    {
        var provider = new Provider { LastName = request.LastName };
        Apply(provider, request);
        database.Providers.Add(provider);
        await database.SaveChangesAsync(cancellationToken);

        return ProviderEndpoints.Respond(provider);
    }

    /// <summary>
    /// Saves over the provider, or answers null where no provider has that id.
    /// </summary>
    public async Task<ProviderResponse?> Update(
        int id,
        SaveProviderRequest request,
        CancellationToken cancellationToken)
    {
        var provider = await database.Providers
            .SingleOrDefaultAsync(provider => provider.Id == id, cancellationToken);

        if (provider is null)
        {
            return null;
        }

        Apply(provider, request);
        await database.SaveChangesAsync(cancellationToken);

        return ProviderEndpoints.Respond(provider);
    }

    public static readonly ReferenceTable<Provider, ProviderResponse> Table = new ProviderTable();

    public Task<string> Export(CancellationToken cancellationToken) =>
        ReferenceImport.Export(database, Table, cancellationToken);

    /// <summary>What importing a CSV file would do, written only where <paramref name="confirm"/> is true.</summary>
    public Task<ReferenceImportResponse> Import(string? csv, bool confirm, CancellationToken cancellationToken) =>
        ReferenceImport.Import(database, Table, csv, confirm, cancellationToken);

    internal static IOrderedQueryable<Provider> Ordered(IQueryable<Provider> providers) =>
        providers
            .OrderBy(provider => provider.LastName)
            .ThenBy(provider => provider.FirstName)
            .ThenBy(provider => provider.Id);

    internal static Task<bool> IsProviderType(SurvsysDbContext database, int id, CancellationToken cancellationToken) =>
        database.CodeValues.AnyAsync(code => code.Id == id && code.List == CodeLists.ProviderType, cancellationToken);

    /// <summary>
    /// The request's values onto the provider: trimmed, blank optional values
    /// stored as null.
    /// </summary>
    internal static void Apply(Provider provider, SaveProviderRequest request)
    {
        provider.LastName = request.LastName.Trim();
        provider.FirstName = Blank(request.FirstName);
        provider.MiddleName = Blank(request.MiddleName);
        provider.ProviderTypeId = request.ProviderTypeId;
        provider.Phone = Blank(request.Phone);
        provider.Email = Blank(request.Email);
        provider.Active = request.Active;
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// The providers, keyed by id: a row whose id is blank is a provider to add,
/// and a row naming an id not on file is refused. A provider has no other key
/// — two may share a name.
/// </summary>
internal sealed class ProviderTable : ReferenceTable<Provider, ProviderResponse>
{
    public override IReadOnlyList<string> Columns => ProviderEndpoints.CsvColumns;

    public override Task<List<ProviderResponse>> List(SurvsysDbContext database, CancellationToken cancellationToken) =>
        ProviderQueries.Ordered(database.Providers)
            .Select(ProviderEndpoints.ToResponse)
            .ToListAsync(cancellationToken);

    public override IReadOnlyList<string?> Fields(ProviderResponse row) => ProviderEndpoints.CsvFields(row);

    public override (ProviderResponse? Row, string? Why) Parse(CsvRow row) => ProviderEndpoints.FromCsv(row);

    public override string? WhyNot(ProviderResponse row) =>
        ProviderEndpoints.WhyNotAProvider(ProviderEndpoints.Request(row));

    public override string KeyOf(ProviderResponse row) => row.Id == 0 ? row.LastName.Trim() : $"{row.Id}";

    public override async Task<Provider?> ByKey(
        SurvsysDbContext database, ProviderResponse row, CancellationToken cancellationToken) =>
        row.Id == 0
            ? null
            : await database.Providers.SingleOrDefaultAsync(provider => provider.Id == row.Id, cancellationToken);

    public override Task<ProviderResponse> RowOf(
        SurvsysDbContext database, Provider entity, CancellationToken cancellationToken) =>
        Task.FromResult(ProviderEndpoints.Respond(entity));

    public override Provider New(ProviderResponse row) => new() { LastName = row.LastName.Trim() };

    public override async Task<string?> Apply(
        SurvsysDbContext database, Provider entity, ProviderResponse row, CancellationToken cancellationToken)
    {
        if (row.Id != entity.Id)
        {
            return $"id {row.Id} is not a provider on file; leave id blank to add a provider.";
        }

        if (row.ProviderTypeId is { } type && !await ProviderQueries.IsProviderType(database, type, cancellationToken))
        {
            return ProviderEndpoints.NotAProviderType(type);
        }

        ProviderQueries.Apply(entity, ProviderEndpoints.Request(row));
        return null;
    }
}
