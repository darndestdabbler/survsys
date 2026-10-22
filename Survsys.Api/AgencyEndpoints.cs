using System.Linq.Expressions;
using System.Net.Mail;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>/api/agencies</c>: the public health agencies a case morbidity report can
/// be assigned to, their administration, and which agency's jurisdiction a zip
/// code falls in.
/// </summary>
/// <remarks>
/// The agencies are the <c>agencies</c> table, which the migration seeds with
/// the sixteen agencies this tier served from a static list before the table
/// existed, under the same identifiers — so every <c>agency_id</c> already
/// written still names the agency it did.
///
/// An agency's jurisdiction zip codes also export as CSV and import from it,
/// by <see cref="ReferenceImport"/>, previewed and then confirmed: an import
/// adds zips and keeps the ones the file leaves out.
///
/// The handlers are handed <see cref="AgencyQueries"/> and not the
/// <c>DbContext</c>, because the architecture test lets no <c>Survsys.Data</c>
/// type into an endpoint signature. The other slices that need to know whether
/// an agency exists ask the same <see cref="AgencyQueries"/>.
/// </remarks>
public static class AgencyEndpoints
{
    private const int NameLength = 200;

    private const int ShortNameLength = 50;

    private const int NotificationEmailsLength = 1000;

    /// <summary>The CSV columns of an agency's jurisdiction zip codes.</summary>
    public static readonly IReadOnlyList<string> ZipCodeColumns = ["zip_code"];

    /// <summary>
    /// The projection from the stored agency to the wire shape a dropdown
    /// reads. Public because the unit suite proves it.
    /// </summary>
    public static readonly Expression<Func<Agency, AgencyResponse>> ToResponse =
        agency => new AgencyResponse(agency.Id, agency.Name);

    /// <summary>
    /// Why this is not an agency this tier will save, or null when it is one.
    /// </summary>
    /// <remarks>
    /// The shape every validator here has: a pure function the unit suite
    /// proves, with the endpoint turning the sentence into a 400. Every zip code
    /// has to be one <see cref="ZipAgencyRules.Normalize"/> reads, so a typo is
    /// refused rather than saved as a jurisdiction nothing resolves into.
    /// </remarks>
    public static string? WhyNotAnAgency(SaveAgencyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "name is required: an agency is chosen from a list by its name.";
        }

        if (request.Name.Trim().Length > NameLength)
        {
            return $"name is longer than {NameLength} characters.";
        }

        if (request.ShortName?.Trim().Length > ShortNameLength)
        {
            return $"shortName is longer than {ShortNameLength} characters.";
        }

        foreach (var zip in request.ZipCodes ?? [])
        {
            if (ZipAgencyRules.Normalize(zip) is null)
            {
                return $"zipCodes holds '{zip}', which is not a zip code. A zip code is five "
                    + "digits, or a ZIP+4 such as 84108-1234.";
            }
        }

        if (NotificationEmails(request.NotificationEmails)?.Length > NotificationEmailsLength)
        {
            return $"notificationEmails is longer than {NotificationEmailsLength} characters.";
        }

        foreach (var address in MessageNotificationService.Addresses(request.NotificationEmails))
        {
            if (!MailAddress.TryCreate(address, out var parsed) || parsed.Address != address)
            {
                return $"notificationEmails holds '{address}', which is not an e-mail address.";
            }
        }

        return null;
    }

    /// <summary>
    /// The notification addresses a save request names, as the agency stores
    /// them: separated by "; ", or null where it names none.
    /// </summary>
    public static string? NotificationEmails(string? requested) =>
        MessageNotificationService.Addresses(requested) is { Count: > 0 } addresses
            ? string.Join("; ", addresses)
            : null;

    /// <summary>
    /// The zip codes a save request names, as the mapping stores them: five
    /// digits each, once each, in order.
    /// </summary>
    /// <remarks>
    /// The request is validated before this is called, so every value reads.
    /// Public because the unit suite proves it.
    /// </remarks>
    public static IReadOnlyList<string> ZipCodesOf(SaveAgencyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return (request.ZipCodes ?? [])
            .Select(ZipAgencyRules.Normalize)
            .OfType<string>()
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The sentence every slice refuses an agency identifier with, whether it
    /// is one nothing can have or one no agency on file has.
    /// </summary>
    public static string NotAnAgency(int agencyId) =>
        $"agencyId {agencyId} is not an agency this tier knows. The agencies are the ones "
            + "GET /api/agencies lists.";

    /// <summary>
    /// Registers what the agency endpoints, and every slice that checks an
    /// agency, need of the container.
    /// </summary>
    public static IServiceCollection AddAgencies(this IServiceCollection services) =>
        services.AddScoped<AgencyQueries>();

    public static IEndpointRouteBuilder MapAgencies(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/agencies", GetAgencies)
            .WithName("GetAgencies")
            .WithSummary("The public health agencies a case morbidity report can be "
                + "assigned to, ordered by name.");

        routes.MapGet("/api/agencies/{id:int}", GetAgency)
            .WithName("GetAgency")
            .WithSummary("One agency, with the zip codes of its jurisdiction.");

        routes.MapPost("/api/agencies", CreateAgency)
            .WithName("CreateAgency")
            .WithSummary("Creates an agency and its jurisdiction's zip codes.");

        routes.MapPut("/api/agencies/{id:int}", UpdateAgency)
            .WithName("UpdateAgency")
            .WithSummary("Saves over an agency and replaces its jurisdiction's zip codes.");

        routes.MapGet("/api/agencies/by-zip/{zip}", GetAgencyByZip)
            .WithName("GetAgencyByZip")
            .WithSummary("The one agency whose jurisdiction a zip code falls in.");

        routes.MapGet("/api/agencies/{id:int}/zip-codes/csv", ExportZipCodes)
            .WithName("ExportAgencyZipCodes")
            .WithSummary("An agency's jurisdiction zip codes, as CSV.");

        routes.MapPost("/api/agencies/{id:int}/zip-codes/csv", ImportZipCodes)
            .WithName("ImportAgencyZipCodes")
            .WithSummary("Previews, or with confirm=true saves, the zip codes of a CSV file added to an "
                + "agency's jurisdiction; a zip the file leaves out is kept.");

        return routes;
    }

    private static async Task<Results<ContentHttpResult, ProblemHttpResult>> ExportZipCodes(
        AgencyQueries agencies,
        int id,
        CancellationToken cancellationToken = default) =>
        await agencies.Find(id, cancellationToken) is null
            ? NoSuchAgency(id)
            : TypedResults.Text(await agencies.ExportZipCodes(id, cancellationToken), "text/csv; charset=utf-8");

    private static async Task<Results<Ok<ReferenceImportResponse>, ProblemHttpResult>> ImportZipCodes(
        AgencyQueries agencies,
        int id,
        HttpRequest request,
        bool confirm = false,
        CancellationToken cancellationToken = default)
    {
        if (await agencies.Find(id, cancellationToken) is null)
        {
            return NoSuchAgency(id);
        }

        using var reader = new StreamReader(request.Body);
        var csv = await reader.ReadToEndAsync(cancellationToken);

        return TypedResults.Ok(await agencies.ImportZipCodes(id, csv, confirm, cancellationToken));
    }

    /// <remarks>
    /// It takes no page: the agencies are a handful, and a dropdown that had to
    /// ask for a second page would be one nobody could use. It still answers in
    /// <see cref="PagedResult{T}"/>, the one collection shape this tier puts on
    /// the wire, with everything on the one page.
    /// </remarks>
    private static async Task<Ok<PagedResult<AgencyResponse>>> GetAgencies(
        AgencyQueries agencies,
        CancellationToken cancellationToken = default)
    {
        var all = await agencies.All(cancellationToken);

        return TypedResults.Ok(new PagedResult<AgencyResponse>(all, Paging.DefaultPage, all.Count, all.Count));
    }

    private static async Task<Results<Ok<AgencyDetailResponse>, ProblemHttpResult>> GetAgency(
        AgencyQueries agencies,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (await agencies.Detail(id, cancellationToken) is not { } agency)
        {
            return NoSuchAgency(id);
        }

        return TypedResults.Ok(agency);
    }

    private static async Task<Results<Created<AgencyDetailResponse>, ProblemHttpResult>> CreateAgency(
        AgencyQueries agencies,
        SaveAgencyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotAnAgency(request) is { } why)
        {
            return Refused(why);
        }

        var created = await agencies.Add(request, cancellationToken);

        return TypedResults.Created($"/api/agencies/{created.Id}", created);
    }

    private static async Task<Results<Ok<AgencyDetailResponse>, ProblemHttpResult>> UpdateAgency(
        AgencyQueries agencies,
        int id,
        SaveAgencyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotAnAgency(request) is { } why)
        {
            return Refused(why);
        }

        if (await agencies.Update(id, request, cancellationToken) is not { } agency)
        {
            return NoSuchAgency(id);
        }

        return TypedResults.Ok(agency);
    }

    /// <remarks>
    /// Each outcome of <see cref="ZipAgencyRules.Resolve"/> is its own status:
    /// a value that is not a zip is a 400, a zip no agency claims a 404, and a
    /// zip two agencies claim a 409 — it names a conflict in the mapping that a
    /// person has to settle, not an agency.
    /// </remarks>
    private static async Task<Results<Ok<AgencyResponse>, ProblemHttpResult>> GetAgencyByZip(
        AgencyQueries agencies,
        string zip,
        CancellationToken cancellationToken = default)
    {
        var resolution = await agencies.Resolve(zip, cancellationToken);

        return resolution.Outcome switch
        {
            ZipResolutionOutcome.NotAZip => TypedResults.Problem(
                detail: $"'{zip}' is not a zip code. A zip code is five digits, or a ZIP+4 "
                    + "such as 84108-1234.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "The value asked about is not a zip code."),
            ZipResolutionOutcome.Unmapped => TypedResults.Problem(
                detail: $"No agency's jurisdiction includes zip code {zip}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "The zip code is not mapped to an agency."),
            ZipResolutionOutcome.Ambiguous => TypedResults.Problem(
                detail: $"More than one agency's jurisdiction includes zip code {zip}, so it "
                    + "names no single agency.",
                statusCode: StatusCodes.Status409Conflict,
                title: "The zip code is mapped to more than one agency."),
            _ => TypedResults.Ok((await agencies.Find(resolution.AgencyId!.Value, cancellationToken))!),
        };
    }

    private static ProblemHttpResult NoSuchAgency(int id) =>
        TypedResults.Problem(
            detail: $"No agency has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such agency.");

    private static ProblemHttpResult Refused(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The agency asked for is not one this endpoint can save.");
}

/// <summary>
/// The agency queries, and the only thing in this tier that holds the
/// <c>DbContext</c> for agencies and the zip-to-agency mapping.
/// </summary>
/// <remarks>
/// Internal, as every slice's queries are. The person-condition and work-queue
/// slices ask <see cref="Find"/> whether an agency a caller named exists.
/// </remarks>
internal sealed class AgencyQueries(SurvsysDbContext database)
{
    /// <summary>Every agency, ordered by name and then by id.</summary>
    public Task<List<AgencyResponse>> All(CancellationToken cancellationToken) =>
        database.Agencies
            .OrderBy(agency => agency.Name)
            .ThenBy(agency => agency.Id)
            .Select(AgencyEndpoints.ToResponse)
            .ToListAsync(cancellationToken);

    /// <summary>The agency with that id, or null where no agency has it.</summary>
    public Task<AgencyResponse?> Find(int id, CancellationToken cancellationToken) =>
        database.Agencies
            .Where(agency => agency.Id == id)
            .Select(AgencyEndpoints.ToResponse)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// The agency with that id and its zip codes, or null where no agency has
    /// it.
    /// </summary>
    public async Task<AgencyDetailResponse?> Detail(int id, CancellationToken cancellationToken)
    {
        var agency = await database.Agencies
            .SingleOrDefaultAsync(agency => agency.Id == id, cancellationToken);

        if (agency is null)
        {
            return null;
        }

        var zipCodes = await database.ZipCodeAgencies
            .Where(mapping => mapping.AgencyId == id)
            .OrderBy(mapping => mapping.ZipCode)
            .Select(mapping => mapping.ZipCode)
            .ToListAsync(cancellationToken);

        return new AgencyDetailResponse(agency.Id, agency.Name, agency.ShortName, zipCodes, agency.NotificationEmails);
    }

    /// <summary>
    /// Creates the agency and its zip codes in one transaction: two saves,
    /// because the mapping rows need the identity the first one assigns.
    /// </summary>
    public async Task<AgencyDetailResponse> Add(SaveAgencyRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var agency = new Agency { Name = request.Name };
        Apply(agency, request);
        database.Agencies.Add(agency);
        await database.SaveChangesAsync(cancellationToken);

        var zipCodes = AgencyEndpoints.ZipCodesOf(request);
        database.ZipCodeAgencies.AddRange(
            zipCodes.Select(zip => new ZipCodeAgency { ZipCode = zip, AgencyId = agency.Id }));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AgencyDetailResponse(agency.Id, agency.Name, agency.ShortName, zipCodes, agency.NotificationEmails);
    }

    /// <summary>
    /// Saves over the agency and replaces its zip codes, or answers null where
    /// no agency has that id.
    /// </summary>
    public async Task<AgencyDetailResponse?> Update(
        int id,
        SaveAgencyRequest request,
        CancellationToken cancellationToken)
    {
        var agency = await database.Agencies
            .SingleOrDefaultAsync(agency => agency.Id == id, cancellationToken);

        if (agency is null)
        {
            return null;
        }

        Apply(agency, request);

        // The difference and not a delete-all-then-insert: a zip kept on the
        // list is one row EF Core already tracks, and adding a second instance
        // under the same key would be refused.
        var zipCodes = AgencyEndpoints.ZipCodesOf(request);
        var stored = await database.ZipCodeAgencies
            .Where(mapping => mapping.AgencyId == id)
            .ToListAsync(cancellationToken);

        database.ZipCodeAgencies.RemoveRange(stored.Where(mapping => !zipCodes.Contains(mapping.ZipCode)));
        database.ZipCodeAgencies.AddRange(zipCodes
            .Except(stored.Select(mapping => mapping.ZipCode))
            .Select(zip => new ZipCodeAgency { ZipCode = zip, AgencyId = id }));
        await database.SaveChangesAsync(cancellationToken);

        return new AgencyDetailResponse(agency.Id, agency.Name, agency.ShortName, zipCodes, agency.NotificationEmails);
    }

    public Task<string> ExportZipCodes(int id, CancellationToken cancellationToken) =>
        ReferenceImport.Export(database, new AgencyZipCodeTable(id), cancellationToken);

    /// <summary>
    /// What adding a CSV file's zip codes to the agency's jurisdiction would
    /// do, written only where <paramref name="confirm"/> is true.
    /// </summary>
    public Task<ReferenceImportResponse> ImportZipCodes(
        int id, string? csv, bool confirm, CancellationToken cancellationToken) =>
        ReferenceImport.Import(database, new AgencyZipCodeTable(id), csv, confirm, cancellationToken);

    /// <summary>
    /// What a zip code resolves to, reading only the mapping rows for that zip.
    /// </summary>
    public async Task<ZipResolution> Resolve(string? zip, CancellationToken cancellationToken)
    {
        if (ZipAgencyRules.Normalize(zip) is not { } code)
        {
            return ZipAgencyRules.Resolve(zip, []);
        }

        var mapping = await database.ZipCodeAgencies
            .Where(row => row.ZipCode == code)
            .ToListAsync(cancellationToken);

        return ZipAgencyRules.Resolve(code, mapping);
    }

    /// <summary>
    /// The request's values onto the agency: trimmed, and a blank short name
    /// stored as null.
    /// </summary>
    private static void Apply(Agency agency, SaveAgencyRequest request)
    {
        agency.Name = request.Name.Trim();
        agency.ShortName = string.IsNullOrWhiteSpace(request.ShortName) ? null : request.ShortName.Trim();
        agency.NotificationEmails = AgencyEndpoints.NotificationEmails(request.NotificationEmails);
    }
}

/// <summary>
/// One agency's jurisdiction zip codes as a reference table: a zip is its own
/// key, so an import adds the zips the agency does not hold, leaves alone the
/// ones it does, and refuses a value that is not a zip code. A zip the file
/// leaves out is kept; one is removed by saving the agency without it.
/// </summary>
internal sealed class AgencyZipCodeTable(int agencyId) : ReferenceTable<ZipCodeAgency, ZipCodeRow>
{
    public override IReadOnlyList<string> Columns => AgencyEndpoints.ZipCodeColumns;

    public override async Task<List<ZipCodeRow>> List(SurvsysDbContext database, CancellationToken cancellationToken) =>
        (await database.ZipCodeAgencies
            .Where(mapping => mapping.AgencyId == agencyId)
            .Select(mapping => mapping.ZipCode)
            .ToListAsync(cancellationToken))
        .Order(StringComparer.Ordinal)
        .Select(zip => new ZipCodeRow(zip, null))
        .ToList();

    public override IReadOnlyList<string?> Fields(ZipCodeRow row) => [row.Code];

    public override (ZipCodeRow? Row, string? Why) Parse(CsvRow row) => (PlaceEndpoints.ZipCode(row) with { County = null }, null);

    public override string? WhyNot(ZipCodeRow row) => PlaceEndpoints.WhyNotAZipCode(row);

    public override string KeyOf(ZipCodeRow row) => row.Code;

    public override Task<ZipCodeAgency?> ByKey(
        SurvsysDbContext database, ZipCodeRow row, CancellationToken cancellationToken) =>
        database.ZipCodeAgencies.SingleOrDefaultAsync(
            mapping => mapping.AgencyId == agencyId && mapping.ZipCode == row.Code, cancellationToken);

    public override Task<ZipCodeRow> RowOf(
        SurvsysDbContext database, ZipCodeAgency entity, CancellationToken cancellationToken) =>
        Task.FromResult(new ZipCodeRow(entity.ZipCode, null));

    public override ZipCodeAgency New(ZipCodeRow row) => new() { ZipCode = row.Code, AgencyId = agencyId };

    public override Task<string?> Apply(
        SurvsysDbContext database, ZipCodeAgency entity, ZipCodeRow row, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);
}
