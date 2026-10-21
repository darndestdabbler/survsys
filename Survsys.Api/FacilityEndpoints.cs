using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>/api/facilities</c>: the facilities list, one facility, and their
/// administration — create and update.
/// </summary>
/// <remarks>
/// The handlers are handed <see cref="FacilityQueries"/> and
/// <see cref="AgencyQueries"/> and not the <c>DbContext</c>, because the
/// architecture test lets no <c>Survsys.Data</c> type into an endpoint
/// signature. An agency no agency on file has is a 400, asked of
/// <see cref="AgencyQueries"/> before the write rather than left to the
/// foreign key to refuse as a 500.
/// </remarks>
public static class FacilityEndpoints
{
    private const int NameLength = 200;

    private const int ShortNameLength = 50;

    private const int StreetLength = 200;

    private const int CityLength = 100;

    private const int PhoneLength = 30;

    /// <summary>
    /// The projection from the stored facility to the wire shape — every
    /// column. Public because the unit suite proves it.
    /// </summary>
    public static readonly Expression<Func<Facility, FacilityResponse>> ToResponse =
        facility => new FacilityResponse(
            facility.Id,
            facility.Name,
            facility.ShortName,
            facility.AgencyId,
            facility.Street,
            facility.City,
            facility.State,
            facility.Zip,
            facility.Phone);

    /// <summary>
    /// <see cref="ToResponse"/> compiled, for a facility just saved and so in
    /// hand rather than in a query.
    /// </summary>
    public static readonly Func<Facility, FacilityResponse> Respond = ToResponse.Compile();

    /// <summary>
    /// Why this is not a facility this tier will save, as far as can be said
    /// without the database, or null when it is one.
    /// </summary>
    /// <remarks>
    /// A pure function the unit suite proves, with the endpoint turning the
    /// sentence into a 400. Whether the agency exists needs a query and is the
    /// endpoint's.
    /// </remarks>
    public static string? WhyNotAFacility(SaveFacilityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "name is required: a facility is chosen from a list by its name.";
        }

        if (request.AgencyId <= 0)
        {
            return "agencyId is required: a facility sits under an agency, one "
                + "GET /api/agencies lists.";
        }

        return TooLong("name", request.Name, NameLength)
            ?? TooLong("shortName", request.ShortName, ShortNameLength)
            ?? TooLong("street", request.Street, StreetLength)
            ?? TooLong("city", request.City, CityLength)
            ?? TooLong("phone", request.Phone, PhoneLength)
            ?? WhyNotAState(request.State)
            ?? WhyNotAZip(request.Zip);
    }

    /// <summary>
    /// Registers what the facility endpoints need of the container.
    /// </summary>
    public static IServiceCollection AddFacilities(this IServiceCollection services) =>
        services.AddScoped<FacilityQueries>();

    public static IEndpointRouteBuilder MapFacilities(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/facilities", GetFacilities)
            .WithName("GetFacilities")
            .WithSummary("The facilities, paged and ordered by name.");

        routes.MapGet("/api/facilities/{id:int}", GetFacility)
            .WithName("GetFacility")
            .WithSummary("One facility.");

        routes.MapPost("/api/facilities", CreateFacility)
            .WithName("CreateFacility")
            .WithSummary("Creates a facility.");

        routes.MapPut("/api/facilities/{id:int}", UpdateFacility)
            .WithName("UpdateFacility")
            .WithSummary("Saves over a facility.");

        routes.MapGet("/api/facilities/csv", async (FacilityQueries facilities, CancellationToken cancellationToken) =>
                TypedResults.Text(await facilities.Export(cancellationToken), "text/csv; charset=utf-8"))
            .WithName("ExportFacilities")
            .WithSummary("Every facility, as CSV.");

        routes.MapPost("/api/facilities/csv", async (
                FacilityQueries facilities,
                HttpRequest request,
                CancellationToken cancellationToken,
                bool confirm = false) =>
            {
                using var reader = new StreamReader(request.Body);
                var csv = await reader.ReadToEndAsync(cancellationToken);
                return TypedResults.Ok(await facilities.Import(csv, confirm, cancellationToken));
            })
            .WithName("ImportFacilities")
            .WithSummary("Previews, or with confirm=true saves, each facility of a CSV file, added where its id "
                + "is blank and saved over by its id otherwise.");

        return routes;
    }

    /// <summary>The facilities' CSV columns, in the order an export writes them.</summary>
    public static readonly IReadOnlyList<string> CsvColumns =
        ["id", "name", "short_name", "agency_id", "street", "city", "state", "zip", "phone"];

    /// <summary>A facility as its CSV fields, in <see cref="CsvColumns"/> order; an id of 0 is blank.</summary>
    public static IReadOnlyList<string?> CsvFields(FacilityResponse row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return
        [
            ReferenceCsv.Number(row.Id == 0 ? null : row.Id), row.Name, row.ShortName,
            ReferenceCsv.Number(row.AgencyId), row.Street, row.City, row.State, row.Zip, row.Phone,
        ];
    }

    /// <summary>
    /// A facility read from its CSV row, a blank id read as 0 — a facility to
    /// add — or why it cannot be read.
    /// </summary>
    public static (FacilityResponse? Row, string? Why) FromCsv(CsvRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (ReferenceCsv.Optional(row, "id", out var id) is { } badId)
        {
            return (null, badId);
        }

        if (ReferenceCsv.Required(row, "agency_id", out var agencyId) is { } badAgency)
        {
            return (null, badAgency);
        }

        return (new FacilityResponse(
            id ?? 0,
            row.Value("name") ?? "",
            row.Value("short_name"),
            agencyId,
            row.Value("street"),
            row.Value("city"),
            row.Value("state"),
            row.Value("zip"),
            row.Value("phone")), null);
    }

    /// <summary>A facility row as the request that saves it.</summary>
    public static SaveFacilityRequest Request(FacilityResponse row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return new(row.Name, row.AgencyId, row.ShortName, row.Street, row.City, row.State, row.Zip, row.Phone);
    }

    private static async Task<Results<Ok<PagedResult<FacilityResponse>>, ProblemHttpResult>> GetFacilities(
        FacilityQueries facilities,
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

        return TypedResults.Ok(await facilities.Page(page, pageSize, cancellationToken));
    }

    private static async Task<Results<Ok<FacilityResponse>, ProblemHttpResult>> GetFacility(
        FacilityQueries facilities,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (await facilities.Find(id, cancellationToken) is not { } facility)
        {
            return NoSuchFacility(id);
        }

        return TypedResults.Ok(facility);
    }

    private static async Task<Results<Created<FacilityResponse>, ProblemHttpResult>> CreateFacility(
        FacilityQueries facilities,
        AgencyQueries agencies,
        SaveFacilityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await WhyNotSaved(request, agencies, cancellationToken) is { } why)
        {
            return Refused(why);
        }

        var created = await facilities.Add(request, cancellationToken);

        return TypedResults.Created($"/api/facilities/{created.Id}", created);
    }

    private static async Task<Results<Ok<FacilityResponse>, ProblemHttpResult>> UpdateFacility(
        FacilityQueries facilities,
        AgencyQueries agencies,
        int id,
        SaveFacilityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await WhyNotSaved(request, agencies, cancellationToken) is { } why)
        {
            return Refused(why);
        }

        if (await facilities.Update(id, request, cancellationToken) is not { } facility)
        {
            return NoSuchFacility(id);
        }

        return TypedResults.Ok(facility);
    }

    private static async Task<string?> WhyNotSaved(
        SaveFacilityRequest request,
        AgencyQueries agencies,
        CancellationToken cancellationToken) =>
        WhyNotAFacility(request)
            ?? (await agencies.Find(request.AgencyId, cancellationToken) is null
                ? AgencyEndpoints.NotAnAgency(request.AgencyId)
                : null);

    private static string? TooLong(string field, string? value, int length) =>
        value?.Trim().Length > length ? $"{field} is longer than {length} characters." : null;

    private static string? WhyNotAState(string? state) =>
        string.IsNullOrWhiteSpace(state) || state.Trim() is { Length: 2 } code && code.All(char.IsAsciiLetter)
            ? null
            : $"state '{state}' is not a two-letter state code.";

    private static string? WhyNotAZip(string? zip) =>
        string.IsNullOrWhiteSpace(zip) || ZipAgencyRules.Normalize(zip) is not null
            ? null
            : $"zip '{zip}' is not a zip code. A zip code is five digits, or a ZIP+4 such as "
                + "84108-1234.";

    private static ProblemHttpResult NoSuchFacility(int id) =>
        TypedResults.Problem(
            detail: $"No facility has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such facility.");

    private static ProblemHttpResult Refused(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The facility asked for is not one this endpoint can save.");
}

/// <summary>
/// The facility queries, and the only thing in this tier that holds the
/// <c>DbContext</c> for facilities.
/// </summary>
internal sealed class FacilityQueries(SurvsysDbContext database)
{
    /// <summary>
    /// One page of facilities, ordered by name and then by id so the order is
    /// total.
    /// </summary>
    public async Task<PagedResult<FacilityResponse>> Page(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var total = await database.Facilities.CountAsync(cancellationToken);

        var rows = await database.Facilities
            .OrderBy(facility => facility.Name)
            .ThenBy(facility => facility.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(FacilityEndpoints.ToResponse)
            .ToListAsync(cancellationToken);

        return new PagedResult<FacilityResponse>(rows, page, pageSize, total);
    }

    /// <summary>The facility with that id, or null where no facility has it.</summary>
    public Task<FacilityResponse?> Find(int id, CancellationToken cancellationToken) =>
        database.Facilities
            .Where(facility => facility.Id == id)
            .Select(FacilityEndpoints.ToResponse)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<FacilityResponse> Add(SaveFacilityRequest request, CancellationToken cancellationToken)
    {
        var facility = new Facility { Name = request.Name };
        Apply(facility, request);
        database.Facilities.Add(facility);
        await database.SaveChangesAsync(cancellationToken);

        return FacilityEndpoints.Respond(facility);
    }

    /// <summary>
    /// Saves over the facility, or answers null where no facility has that id.
    /// </summary>
    public async Task<FacilityResponse?> Update(
        int id,
        SaveFacilityRequest request,
        CancellationToken cancellationToken)
    {
        var facility = await database.Facilities
            .SingleOrDefaultAsync(facility => facility.Id == id, cancellationToken);

        if (facility is null)
        {
            return null;
        }

        Apply(facility, request);
        await database.SaveChangesAsync(cancellationToken);

        return FacilityEndpoints.Respond(facility);
    }

    public static readonly ReferenceTable<Facility, FacilityResponse> Table = new FacilityTable();

    public Task<string> Export(CancellationToken cancellationToken) =>
        ReferenceImport.Export(database, Table, cancellationToken);

    /// <summary>What importing a CSV file would do, written only where <paramref name="confirm"/> is true.</summary>
    public Task<ReferenceImportResponse> Import(string? csv, bool confirm, CancellationToken cancellationToken) =>
        ReferenceImport.Import(database, Table, csv, confirm, cancellationToken);

    /// <summary>
    /// The request's values onto the facility: trimmed, blank optional values
    /// stored as null, the state upper-cased and the zip as its five digits.
    /// </summary>
    internal static void Apply(Facility facility, SaveFacilityRequest request)
    {
        facility.Name = request.Name.Trim();
        facility.AgencyId = request.AgencyId;
        facility.ShortName = Blank(request.ShortName);
        facility.Street = Blank(request.Street);
        facility.City = Blank(request.City);
        facility.State = Blank(request.State)?.ToUpperInvariant();
        facility.Zip = ZipAgencyRules.Normalize(request.Zip);
        facility.Phone = Blank(request.Phone);
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// The facilities, sending facilities among them, keyed by id: a row whose id
/// is blank is a facility to add, and a row naming an id not on file is
/// refused. A facility has no other key — two may share a name.
/// </summary>
internal sealed class FacilityTable : ReferenceTable<Facility, FacilityResponse>
{
    public override IReadOnlyList<string> Columns => FacilityEndpoints.CsvColumns;

    public override Task<List<FacilityResponse>> List(SurvsysDbContext database, CancellationToken cancellationToken) =>
        database.Facilities
            .OrderBy(facility => facility.Name)
            .ThenBy(facility => facility.Id)
            .Select(FacilityEndpoints.ToResponse)
            .ToListAsync(cancellationToken);

    public override IReadOnlyList<string?> Fields(FacilityResponse row) => FacilityEndpoints.CsvFields(row);

    public override (FacilityResponse? Row, string? Why) Parse(CsvRow row) => FacilityEndpoints.FromCsv(row);

    public override string? WhyNot(FacilityResponse row) =>
        FacilityEndpoints.WhyNotAFacility(FacilityEndpoints.Request(row));

    public override string KeyOf(FacilityResponse row) => row.Id == 0 ? row.Name.Trim() : $"{row.Id}";

    public override async Task<Facility?> ByKey(
        SurvsysDbContext database, FacilityResponse row, CancellationToken cancellationToken) =>
        row.Id == 0
            ? null
            : await database.Facilities.SingleOrDefaultAsync(facility => facility.Id == row.Id, cancellationToken);

    public override Task<FacilityResponse> RowOf(
        SurvsysDbContext database, Facility entity, CancellationToken cancellationToken) =>
        Task.FromResult(FacilityEndpoints.Respond(entity));

    public override Facility New(FacilityResponse row) => new() { Name = row.Name.Trim() };

    public override async Task<string?> Apply(
        SurvsysDbContext database, Facility entity, FacilityResponse row, CancellationToken cancellationToken)
    {
        if (row.Id != entity.Id)
        {
            return $"id {row.Id} is not a facility on file; leave id blank to add a facility.";
        }

        if (!await database.Agencies.AnyAsync(agency => agency.Id == row.AgencyId, cancellationToken))
        {
            return AgencyEndpoints.NotAnAgency(row.AgencyId);
        }

        FacilityQueries.Apply(entity, FacilityEndpoints.Request(row));
        return null;
    }
}
