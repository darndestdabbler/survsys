using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>POST /api/person-conditions/{id}/exposures</c>: records an exposure
/// event against a report, with the place it happened, the contacts who were
/// there and the sources it came from. <c>GET /api/person-conditions/{id}/exposures</c>:
/// that report's exposures, paged. And <c>GET /api/exposures/{id}</c>: one
/// exposure whole.
/// </summary>
/// <remarks>
/// Modelled on <see cref="LabEndpoints"/>, slice for slice. The entity-to-wire
/// mapping lives here, as the other slices' does and for the same reason:
/// <c>Survsys.Data</c> may not reference <c>Survsys.Contracts</c>. The handlers
/// are handed <see cref="ExposureQueries"/> and not the <c>DbContext</c>,
/// because the architecture test lets no <c>Survsys.Data</c> type into an
/// endpoint signature at all.
///
/// An exposure is read whole by composing queries rather than by walking
/// navigations the entities do not have: the exposure, its place, its contacts
/// and its sources are four reads this tier stitches together, which keeps the
/// entities free of navigation a later session would otherwise inherit
/// unasked.
///
/// An exposure is scoped to the report in the route: the server sets
/// <c>person_condition_id</c> to that report and reads <c>condition_id</c> and
/// <c>agency_id</c> off it, never taking any of the three on the wire, as the
/// laboratory slice sets a specimen's person. Recording an exposure writes
/// across four tables, so it is done inside one transaction: the exposure, its
/// place, its contacts and its sources either all land or none do. There is no
/// PUT and no DELETE over a stored exposure, and no endpoint of a contact's
/// disposition's own: an exposure is recorded whole and listed, as a specimen
/// is.
/// </remarks>
public static class ExposureEndpoints
{
    /// <summary>The projection from a stored exposure to its list shape.</summary>
    /// <remarks>
    /// An <see cref="Expression"/>, as the other slices' projections are: EF
    /// Core translates it into the <c>SELECT</c> of the list, and a unit test
    /// compiles and invokes it to prove what it carries. Public because the
    /// unit suite proves it.
    /// </remarks>
    public static readonly Expression<Func<Exposure, ExposureResponse>> ExposureToResponse =
        exposure => new ExposureResponse(
            exposure.Id,
            exposure.Name,
            exposure.Description,
            exposure.ExposureStart,
            exposure.ExposureEnd,
            exposure.ExposurePlaceId);

    /// <summary>The projection from a stored place to its wire shape.</summary>
    public static readonly Expression<Func<ExposurePlace, ExposurePlaceResponse>> PlaceToResponse =
        place => new ExposurePlaceResponse(
            place.Id,
            place.ExposurePlaceTypeId,
            place.Name,
            place.Street,
            place.City,
            place.Zip,
            place.State,
            place.Latitude,
            place.Longitude,
            place.Phone);

    /// <summary>The projection from a stored contact to its wire shape.</summary>
    public static readonly Expression<Func<ExposureContact, ExposureContactResponse>> ContactToResponse =
        contact => new ExposureContactResponse(
            contact.Id,
            contact.ExposureId,
            contact.PersonId,
            contact.ExposureStart,
            contact.ExposureEnd,
            contact.DispositionId,
            contact.ContactType,
            contact.DispositionDate);

    /// <summary>The projection from a stored source to its wire shape.</summary>
    public static readonly Expression<Func<ExposureSource, ExposureSourceResponse>> SourceToResponse =
        source => new ExposureSourceResponse(
            source.Id,
            source.ExposureId,
            source.PersonId,
            source.PersonConditionId,
            source.SourceName,
            source.SourceType);

    /// <summary>
    /// The projection from a stored exposure and its already-read place,
    /// contacts and sources to the exposure's whole shape.
    /// </summary>
    /// <remarks>
    /// Four parameters because an exposure on the wire carries what hangs off
    /// it, which is read by its own queries and passed in rather than reached
    /// through a navigation: this maps the exposure's own columns and hangs the
    /// rest on them. Applied in memory — compiled into <see cref="RespondDetail"/>
    /// — and not translated, because what it is handed came from the database
    /// already. Public because the unit suite proves it.
    /// </remarks>
    public static readonly Expression<Func<
        Exposure,
        ExposurePlaceResponse?,
        IReadOnlyList<ExposureContactResponse>,
        IReadOnlyList<ExposureSourceResponse>,
        ExposureDetailResponse>> ExposureToDetail =
        (exposure, place, contacts, sources) => new ExposureDetailResponse(
            exposure.Id,
            exposure.PersonConditionId,
            exposure.ConditionId,
            exposure.AgencyId,
            exposure.Name,
            exposure.Description,
            exposure.ExposureStart,
            exposure.ExposureEnd,
            place,
            contacts,
            sources);

    /// <summary><see cref="ExposureToDetail"/> compiled, for the composition.</summary>
    public static readonly Func<
        Exposure,
        ExposurePlaceResponse?,
        IReadOnlyList<ExposureContactResponse>,
        IReadOnlyList<ExposureSourceResponse>,
        ExposureDetailResponse> RespondDetail = ExposureToDetail.Compile();

    /// <summary>
    /// Why the exposure asked to be recorded is not one this endpoint can
    /// record, as far as can be said without the database, or null when nothing
    /// is wrong with it.
    /// </summary>
    /// <remarks>
    /// The shape <see cref="LabEndpoints.WhyNotALab"/> has: a pure function
    /// the unit suite proves, with the endpoint turning the sentence into a 400
    /// that names the field. Whether the report exists, and whether each
    /// contact's and source's person does, is not here, because that needs a
    /// query — <see cref="ExposureQueries"/> is where those live. What is here
    /// is what is a contradiction on its face: a time range that ends before
    /// it starts, on the exposure or on any contact, where both ends are
    /// given; and a contact that names no person at all, because a contact is
    /// a person's record against the exposure and the column that holds the
    /// person is not nullable.
    ///
    /// Public because the unit suite proves it; nothing outside this file calls
    /// it.
    /// </remarks>
    public static string? WhyNotAnExposure(CreateExposureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request is { ExposureStart: { } start, ExposureEnd: { } end } && end < start)
        {
            return $"exposureEnd {end:yyyy-MM-dd} is before exposureStart {start:yyyy-MM-dd}, and "
                + "an exposure ends no earlier than it starts. Give the dates in order, or leave "
                + "one out.";
        }

        var contacts = request.Contacts ?? [];

        for (var at = 0; at < contacts.Count; at++)
        {
            var contact = contacts[at];

            if (contact.PersonId is null)
            {
                return $"contacts[{at}].personId is missing, and a contact is the record of an "
                    + "existing person against the exposure: name the person, by the id a person "
                    + "search returns.";
            }

            if (contact is { ExposureStart: { } from, ExposureEnd: { } to } && to < from)
            {
                return $"contacts[{at}].exposureEnd {to:yyyy-MM-dd} is before "
                    + $"contacts[{at}].exposureStart {from:yyyy-MM-dd}, and a contact's time at "
                    + "an exposure ends no earlier than it starts. Give the dates in order, or "
                    + "leave one out.";
            }
        }

        return null;
    }

    /// <summary>
    /// Why an exposure correction is not one this endpoint can make, as far as
    /// can be said without the database, or null when nothing is wrong with it.
    /// </summary>
    /// <remarks>
    /// The database-free half of <see cref="WhyNotAnExposure"/> over an
    /// exposure's own columns: a time range that ends before it starts, where
    /// both ends are given. Public because the unit suite proves it; nothing
    /// outside this file calls it.
    /// </remarks>
    public static string? WhyNotAnExposureEdit(UpdateExposureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request is { ExposureStart: { } start, ExposureEnd: { } end } && end < start)
        {
            return $"exposureEnd {end:yyyy-MM-dd} is before exposureStart {start:yyyy-MM-dd}, and "
                + "an exposure ends no earlier than it starts. Give the dates in order, or leave "
                + "one out.";
        }

        return null;
    }

    /// <summary>
    /// Why a contact correction is not one this endpoint can make, as far as
    /// can be said without the database, or null when nothing is wrong with it.
    /// </summary>
    /// <remarks>
    /// The database-free half of <see cref="WhyNotAnExposure"/> over a single
    /// contact: a contact names a person — the column that holds it is not
    /// nullable, so an edit may not drop it — and its time at the exposure ends
    /// no earlier than it starts, where both ends are given. Whether the person
    /// named is on file is not here, because that needs a query. Public because
    /// the unit suite proves it; nothing outside this file calls it.
    /// </remarks>
    public static string? WhyNotAContactEdit(ExposureContactRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PersonId is null)
        {
            return "personId is missing, and a contact is the record of an existing person "
                + "against the exposure: name the person, by the id a person search returns.";
        }

        if (request is { ExposureStart: { } from, ExposureEnd: { } to } && to < from)
        {
            return $"exposureEnd {to:yyyy-MM-dd} is before exposureStart {from:yyyy-MM-dd}, and a "
                + "contact's time at an exposure ends no earlier than it starts. Give the dates "
                + "in order, or leave one out.";
        }

        return null;
    }

    /// <summary>
    /// Registers what the exposure endpoints need of the container. Kept here
    /// with the endpoints so the slice's wiring is in one place.
    /// </summary>
    public static IServiceCollection AddExposures(this IServiceCollection services) =>
        services.AddScoped<ExposureQueries>();

    public static IEndpointRouteBuilder MapExposures(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/person-conditions/{id:int}/exposures", CreateExposure)
            .WithName("CreateExposure")
            .WithSummary("Records an exposure event against a case morbidity report.");

        routes.MapGet("/api/person-conditions/{id:int}/exposures", GetExposures)
            .WithName("GetExposures")
            .WithSummary("A case morbidity report's exposure events, paged.");

        routes.MapGet("/api/exposures/{id:int}", GetExposure)
            .WithName("GetExposure")
            .WithSummary("One exposure event, with its place, its contacts and its sources.");

        routes.MapPut("/api/exposures/{id:int}", UpdateExposure)
            .WithName("UpdateExposure")
            .WithSummary("Corrects an exposure's own columns.");

        routes.MapDelete("/api/exposures/{id:int}", DeleteExposure)
            .WithName("DeleteExposure")
            .WithSummary("Removes an exposure that has no contacts or sources.");

        routes.MapPut("/api/exposure-places/{id:int}", UpdateExposurePlace)
            .WithName("UpdateExposurePlace")
            .WithSummary("Corrects an exposure place.");

        routes.MapDelete("/api/exposure-places/{id:int}", DeleteExposurePlace)
            .WithName("DeleteExposurePlace")
            .WithSummary("Removes an exposure place no exposure points at.");

        routes.MapPut("/api/exposure-contacts/{id:int}", UpdateExposureContact)
            .WithName("UpdateExposureContact")
            .WithSummary("Corrects a contact, including its disposition and the date it was dispositioned.");

        routes.MapDelete("/api/exposure-contacts/{id:int}", DeleteExposureContact)
            .WithName("DeleteExposureContact")
            .WithSummary("Removes a contact.");

        routes.MapPut("/api/exposure-sources/{id:int}", UpdateExposureSource)
            .WithName("UpdateExposureSource")
            .WithSummary("Corrects a source.");

        routes.MapDelete("/api/exposure-sources/{id:int}", DeleteExposureSource)
            .WithName("DeleteExposureSource")
            .WithSummary("Removes a source.");

        return routes;
    }

    private static async Task<Results<Created<ExposureDetailResponse>, ProblemHttpResult>> CreateExposure(
        ExposureQueries exposures,
        int id,
        CreateExposureRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotAnExposure(request) is { } why)
        {
            return Refused(why);
        }

        if (!await exposures.HasReport(id, cancellationToken))
        {
            return NoSuchReport(id);
        }

        if (await exposures.WhyNotOnFile(request, cancellationToken) is { } missing)
        {
            return Refused(missing);
        }

        var exposureId = await exposures.Add(id, request, cancellationToken);

        var created = await exposures.Detail(exposureId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"The exposure just recorded as id {exposureId} could not be read back.");

        return TypedResults.Created($"/api/exposures/{exposureId}", created);
    }

    private static async Task<Results<Ok<PagedResult<ExposureResponse>>, ProblemHttpResult>> GetExposures(
        ExposureQueries exposures,
        int id,
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

        if (!await exposures.HasReport(id, cancellationToken))
        {
            return NoSuchReport(id);
        }

        return TypedResults.Ok(await exposures.Page(id, page, pageSize, cancellationToken));
    }

    private static async Task<Results<Ok<ExposureDetailResponse>, ProblemHttpResult>> GetExposure(
        ExposureQueries exposures,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (await exposures.Detail(id, cancellationToken) is not { } detail)
        {
            return TypedResults.Problem(
                detail: $"No exposure has id {id}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such exposure.");
        }

        return TypedResults.Ok(detail);
    }

    private static async Task<Results<Ok<ExposureResponse>, ProblemHttpResult>> UpdateExposure(
        ExposureQueries exposures,
        int id,
        UpdateExposureRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotAnExposureEdit(request) is { } why)
        {
            return Refused(why);
        }

        if (await exposures.UpdateExposure(id, request, cancellationToken) is not { } exposure)
        {
            return NoSuchExposure(id);
        }

        return TypedResults.Ok(exposure);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteExposure(
        ExposureQueries exposures,
        int id,
        CancellationToken cancellationToken = default)
    {
        var (found, blocked) = await exposures.DeleteExposure(id, cancellationToken);

        if (!found)
        {
            return NoSuchExposure(id);
        }

        return blocked is { } why ? Dependent(why) : TypedResults.NoContent();
    }

    private static async Task<Results<Ok<ExposurePlaceResponse>, ProblemHttpResult>> UpdateExposurePlace(
        ExposureQueries exposures,
        int id,
        ExposurePlaceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await exposures.UpdatePlace(id, request, cancellationToken) is not { } place)
        {
            return NoSuchPlace(id);
        }

        return TypedResults.Ok(place);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteExposurePlace(
        ExposureQueries exposures,
        int id,
        CancellationToken cancellationToken = default)
    {
        var (found, blocked) = await exposures.DeletePlace(id, cancellationToken);

        if (!found)
        {
            return NoSuchPlace(id);
        }

        return blocked is { } why ? Dependent(why) : TypedResults.NoContent();
    }

    private static async Task<Results<Ok<ExposureContactResponse>, ProblemHttpResult>> UpdateExposureContact(
        ExposureQueries exposures,
        int id,
        ExposureContactRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotAContactEdit(request) is { } why)
        {
            return Refused(why);
        }

        if (request.PersonId is { } personId
            && !await exposures.PersonOnFile(personId, cancellationToken))
        {
            return Refused($"personId {personId} is not a person on file. A contact is the record "
                + "of an existing person against the exposure: find the person first, with "
                + "GET /api/persons.");
        }

        if (await exposures.UpdateContact(id, request, cancellationToken) is not { } contact)
        {
            return NoSuchContact(id);
        }

        return TypedResults.Ok(contact);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteExposureContact(
        ExposureQueries exposures,
        int id,
        CancellationToken cancellationToken = default) =>
        await exposures.DeleteContact(id, cancellationToken)
            ? TypedResults.NoContent()
            : NoSuchContact(id);

    private static async Task<Results<Ok<ExposureSourceResponse>, ProblemHttpResult>> UpdateExposureSource(
        ExposureQueries exposures,
        int id,
        ExposureSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.PersonId is { } personId
            && !await exposures.PersonOnFile(personId, cancellationToken))
        {
            return Refused($"personId {personId} is not a person on file. A source that names a "
                + "person names one that exists: find the person first, with GET /api/persons, or "
                + "name the source by sourceName alone.");
        }

        if (await exposures.UpdateSource(id, request, cancellationToken) is not { } source)
        {
            return NoSuchSource(id);
        }

        return TypedResults.Ok(source);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteExposureSource(
        ExposureQueries exposures,
        int id,
        CancellationToken cancellationToken = default) =>
        await exposures.DeleteSource(id, cancellationToken)
            ? TypedResults.NoContent()
            : NoSuchSource(id);

    private static ProblemHttpResult Refused(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The exposure asked for is not one this endpoint can record.");

    private static ProblemHttpResult Dependent(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status409Conflict,
            title: "The row still has rows recorded against it and cannot be removed.");

    private static ProblemHttpResult NoSuchReport(int id) =>
        TypedResults.Problem(
            detail: $"No case morbidity report has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such case morbidity report.");

    private static ProblemHttpResult NoSuchExposure(int id) =>
        TypedResults.Problem(
            detail: $"No exposure has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such exposure.");

    private static ProblemHttpResult NoSuchPlace(int id) =>
        TypedResults.Problem(
            detail: $"No exposure place has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such exposure place.");

    private static ProblemHttpResult NoSuchContact(int id) =>
        TypedResults.Problem(
            detail: $"No contact has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such contact.");

    private static ProblemHttpResult NoSuchSource(int id) =>
        TypedResults.Problem(
            detail: $"No source has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such source.");
}

/// <summary>
/// The exposure queries and the recording, and the only thing in this tier
/// that holds the <c>DbContext</c> for them.
/// </summary>
/// <remarks>
/// Internal, and in this file rather than its own, for the reason
/// <c>LabQueries</c> is: it exists only to serve <see cref="ExposureEndpoints"/>,
/// and it is the named step between an endpoint signature and
/// <c>Survsys.Data</c> that the architecture test requires.
/// </remarks>
internal sealed class ExposureQueries(SurvsysDbContext database)
{
    /// <summary>
    /// Whether a report has that id — false is the answer the endpoint turns
    /// into a 404, because the report is the resource the route names.
    /// </summary>
    public Task<bool> HasReport(int reportId, CancellationToken cancellationToken) =>
        database.PersonConditions.AnyAsync(report => report.Id == reportId, cancellationToken);

    /// <summary>
    /// Why a person the exposure's contacts or sources name is not one to
    /// record against, or null when every person named is on file.
    /// </summary>
    /// <remarks>
    /// One read for every person id the request names, and the first missing
    /// one named with its field — the answer the endpoint turns into a 400,
    /// because the person is a field of the request and not the resource the
    /// route names. A contact with no person at all never reaches here:
    /// <see cref="ExposureEndpoints.WhyNotAnExposure"/> refuses it first. A
    /// source may name no person, which is not a fault; one that does is
    /// checked as a contact's is.
    /// </remarks>
    public async Task<string?> WhyNotOnFile(
        CreateExposureRequest request,
        CancellationToken cancellationToken)
    {
        var contacts = request.Contacts ?? [];
        var sources = request.Sources ?? [];

        var named = contacts.Select(contact => contact.PersonId)
            .Concat(sources.Select(source => source.PersonId))
            .OfType<int>()
            .Distinct()
            .ToList();

        if (named.Count == 0)
        {
            return null;
        }

        var onFile = await database.Persons
            .Where(person => named.Contains(person.Id))
            .Select(person => person.Id)
            .ToListAsync(cancellationToken);

        for (var at = 0; at < contacts.Count; at++)
        {
            if (contacts[at].PersonId is { } personId && !onFile.Contains(personId))
            {
                return $"contacts[{at}].personId {personId} is not a person on file. A contact "
                    + "is the record of an existing person against the exposure: find the "
                    + "person first, with GET /api/persons.";
            }
        }

        for (var at = 0; at < sources.Count; at++)
        {
            if (sources[at].PersonId is { } personId && !onFile.Contains(personId))
            {
                return $"sources[{at}].personId {personId} is not a person on file. A source "
                    + "that names a person names one that exists: find the person first, with "
                    + "GET /api/persons, or name the source by sourceName alone.";
            }
        }

        return null;
    }

    /// <summary>
    /// Records the exposure and everything hung off it, scoped to the report,
    /// and answers with the new exposure's id.
    /// </summary>
    /// <remarks>
    /// The condition and the agency are read from the report, because an
    /// exposure is recorded against the report in the route and never takes
    /// its scope on the wire. The writes span four tables and the keys are
    /// database-generated, so they are saved in dependency order inside one
    /// transaction rather than reached through navigations the entities do not
    /// have: the place first, because the exposure points at it; then the
    /// exposure; then its contacts and sources. The report's existence and the
    /// persons' are confirmed by <see cref="HasReport"/> and
    /// <see cref="WhyNotOnFile"/> before this is called.
    /// </remarks>
    public async Task<int> Add(
        int reportId,
        CreateExposureRequest request,
        CancellationToken cancellationToken)
    {
        var scope = await database.PersonConditions
            .Where(report => report.Id == reportId)
            .Select(report => new { report.ConditionId, report.AgencyId })
            .SingleAsync(cancellationToken);

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        int? placeId = null;

        if (request.Place is { } placeRequest)
        {
            var place = new ExposurePlace
            {
                ExposurePlaceTypeId = placeRequest.ExposurePlaceTypeId,
                Name = placeRequest.Name,
                Street = placeRequest.Street,
                City = placeRequest.City,
                Zip = placeRequest.Zip,
                State = placeRequest.State,
                Latitude = placeRequest.Latitude,
                Longitude = placeRequest.Longitude,
                Phone = placeRequest.Phone,
            };

            database.ExposurePlaces.Add(place);
            await database.SaveChangesAsync(cancellationToken);
            placeId = place.Id;
        }

        var exposure = new Exposure
        {
            PersonConditionId = reportId,
            ConditionId = scope.ConditionId,
            AgencyId = scope.AgencyId,
            ExposurePlaceId = placeId,
            ExposureStart = request.ExposureStart,
            ExposureEnd = request.ExposureEnd,
            Name = request.Name,
            Description = request.Description,
        };

        database.Exposures.Add(exposure);
        await database.SaveChangesAsync(cancellationToken);

        foreach (var contactRequest in request.Contacts ?? [])
        {
            database.ExposureContacts.Add(new ExposureContact
            {
                ExposureId = exposure.Id,
                PersonId = contactRequest.PersonId
                    ?? throw new InvalidOperationException(
                        "A contact with no person reached the recording; the validator refuses one."),
                ExposureStart = contactRequest.ExposureStart,
                ExposureEnd = contactRequest.ExposureEnd,
                DispositionId = contactRequest.DispositionId,
                ContactType = contactRequest.ContactType,
                DispositionDate = contactRequest.DispositionDate,
            });
        }

        foreach (var sourceRequest in request.Sources ?? [])
        {
            database.ExposureSources.Add(new ExposureSource
            {
                ExposureId = exposure.Id,
                PersonId = sourceRequest.PersonId,
                PersonConditionId = sourceRequest.PersonConditionId,
                SourceName = sourceRequest.SourceName,
                SourceType = sourceRequest.SourceType,
            });
        }

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return exposure.Id;
    }

    /// <summary>
    /// One page of a report's exposures, ordered by id — a total order, so an
    /// exposure cannot appear on two pages or on neither.
    /// </summary>
    public async Task<PagedResult<ExposureResponse>> Page(
        int reportId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var exposures = database.Exposures
            .Where(exposure => exposure.PersonConditionId == reportId);

        var total = await exposures.CountAsync(cancellationToken);

        var rows = await exposures
            .OrderBy(exposure => exposure.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ExposureEndpoints.ExposureToResponse)
            .ToListAsync(cancellationToken);

        return new PagedResult<ExposureResponse>(rows, page, pageSize, total);
    }

    /// <summary>
    /// One exposure whole — the exposure, its place, its contacts and its
    /// sources — or null where no exposure has that id.
    /// </summary>
    /// <remarks>
    /// Four reads composed in this tier rather than one query over navigations
    /// the entities do not have: the exposure, the place it points at where it
    /// points at one, the contacts and the sources.
    /// </remarks>
    public async Task<ExposureDetailResponse?> Detail(int exposureId, CancellationToken cancellationToken)
    {
        var exposure = await database.Exposures
            .SingleOrDefaultAsync(candidate => candidate.Id == exposureId, cancellationToken);

        if (exposure is null)
        {
            return null;
        }

        var place = exposure.ExposurePlaceId is { } placeId
            ? await database.ExposurePlaces
                .Where(candidate => candidate.Id == placeId)
                .Select(ExposureEndpoints.PlaceToResponse)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        var contacts = await database.ExposureContacts
            .Where(contact => contact.ExposureId == exposureId)
            .OrderBy(contact => contact.Id)
            .Select(ExposureEndpoints.ContactToResponse)
            .ToListAsync(cancellationToken);

        var sources = await database.ExposureSources
            .Where(source => source.ExposureId == exposureId)
            .OrderBy(source => source.Id)
            .Select(ExposureEndpoints.SourceToResponse)
            .ToListAsync(cancellationToken);

        return ExposureEndpoints.RespondDetail(exposure, place, contacts, sources);
    }

    /// <summary>Whether a person has that id — the check a contact's or source's edit makes.</summary>
    public Task<bool> PersonOnFile(int personId, CancellationToken cancellationToken) =>
        database.Persons.AnyAsync(person => person.Id == personId, cancellationToken);

    /// <summary>
    /// Corrects an exposure's own columns — including which place it points at,
    /// by id, or none — and answers with it as stored, or null where no
    /// exposure has that id. The contacts and sources, and a place's own
    /// columns, are corrected through their own routes; here the exposure row
    /// is set, so pointing it at no place is how a place is detached before it
    /// is removed.
    /// </summary>
    public async Task<ExposureResponse?> UpdateExposure(
        int exposureId,
        UpdateExposureRequest request,
        CancellationToken cancellationToken)
    {
        var exposure = await database.Exposures
            .SingleOrDefaultAsync(candidate => candidate.Id == exposureId, cancellationToken);

        if (exposure is null)
        {
            return null;
        }

        exposure.Name = request.Name;
        exposure.Description = request.Description;
        exposure.ExposureStart = request.ExposureStart;
        exposure.ExposureEnd = request.ExposureEnd;
        exposure.ExposurePlaceId = request.ExposurePlaceId;

        await database.SaveChangesAsync(cancellationToken);

        return ExposureEndpoints.ExposureToResponse.Compile().Invoke(exposure);
    }

    /// <summary>
    /// Removes an exposure, or keeps it with the reason where contacts or
    /// sources still hang off it, so the rows recorded against it are never
    /// orphaned by a foreign key doing the refusing. The place the exposure
    /// pointed at is left on file to be removed through its own route.
    /// </summary>
    public async Task<(bool Found, string? Blocked)> DeleteExposure(
        int exposureId,
        CancellationToken cancellationToken)
    {
        var exposure = await database.Exposures
            .SingleOrDefaultAsync(candidate => candidate.Id == exposureId, cancellationToken);

        if (exposure is null)
        {
            return (false, null);
        }

        var contacts = await database.ExposureContacts
            .CountAsync(contact => contact.ExposureId == exposureId, cancellationToken);
        var sources = await database.ExposureSources
            .CountAsync(source => source.ExposureId == exposureId, cancellationToken);

        if (contacts > 0 || sources > 0)
        {
            var parts = new List<string>();
            if (contacts > 0)
            {
                parts.Add(contacts == 1 ? "1 contact" : $"{contacts} contacts");
            }

            if (sources > 0)
            {
                parts.Add(sources == 1 ? "1 source" : $"{sources} sources");
            }

            return (true, $"exposure {exposureId} still has {string.Join(" and ", parts)} recorded "
                + "against it and cannot be removed. Remove those first.");
        }

        database.Exposures.Remove(exposure);
        await database.SaveChangesAsync(cancellationToken);

        return (true, null);
    }

    /// <summary>
    /// Corrects a place and answers with it as stored, or null where no place
    /// has that id.
    /// </summary>
    public async Task<ExposurePlaceResponse?> UpdatePlace(
        int placeId,
        ExposurePlaceRequest request,
        CancellationToken cancellationToken)
    {
        var place = await database.ExposurePlaces
            .SingleOrDefaultAsync(candidate => candidate.Id == placeId, cancellationToken);

        if (place is null)
        {
            return null;
        }

        place.ExposurePlaceTypeId = request.ExposurePlaceTypeId;
        place.Name = request.Name;
        place.Street = request.Street;
        place.City = request.City;
        place.Zip = request.Zip;
        place.State = request.State;
        place.Latitude = request.Latitude;
        place.Longitude = request.Longitude;
        place.Phone = request.Phone;

        await database.SaveChangesAsync(cancellationToken);

        return ExposureEndpoints.PlaceToResponse.Compile().Invoke(place);
    }

    /// <summary>
    /// Removes a place, or keeps it with the reason where an exposure still
    /// points at it: a place is shared by the exposures that name it, and one
    /// removed from under them would leave a dangling reference.
    /// </summary>
    public async Task<(bool Found, string? Blocked)> DeletePlace(
        int placeId,
        CancellationToken cancellationToken)
    {
        var place = await database.ExposurePlaces
            .SingleOrDefaultAsync(candidate => candidate.Id == placeId, cancellationToken);

        if (place is null)
        {
            return (false, null);
        }

        var pointing = await database.Exposures
            .CountAsync(exposure => exposure.ExposurePlaceId == placeId, cancellationToken);

        if (pointing > 0)
        {
            var exposures = pointing == 1 ? "1 exposure" : $"{pointing} exposures";
            var those = pointing == 1 ? "that exposure" : "those exposures";

            return (true, $"exposure place {placeId} is still pointed at by {exposures} and cannot "
                + $"be removed. Point {those} at another place or remove it first.");
        }

        database.ExposurePlaces.Remove(place);
        await database.SaveChangesAsync(cancellationToken);

        return (true, null);
    }

    /// <summary>
    /// Corrects a contact — its disposition and the date it was dispositioned
    /// among its columns — and answers with it as stored, or null where no
    /// contact has that id. The person the contact names is validated before
    /// this is called and is never cleared here.
    /// </summary>
    public async Task<ExposureContactResponse?> UpdateContact(
        int contactId,
        ExposureContactRequest request,
        CancellationToken cancellationToken)
    {
        var contact = await database.ExposureContacts
            .SingleOrDefaultAsync(candidate => candidate.Id == contactId, cancellationToken);

        if (contact is null)
        {
            return null;
        }

        contact.PersonId = request.PersonId
            ?? throw new InvalidOperationException(
                "A contact edit with no person reached the recording; the validator refuses one.");
        contact.ExposureStart = request.ExposureStart;
        contact.ExposureEnd = request.ExposureEnd;
        contact.DispositionId = request.DispositionId;
        contact.ContactType = request.ContactType;
        contact.DispositionDate = request.DispositionDate;

        await database.SaveChangesAsync(cancellationToken);

        return ExposureEndpoints.ContactToResponse.Compile().Invoke(contact);
    }

    /// <summary>Removes a contact — a leaf — and says whether it was there.</summary>
    public async Task<bool> DeleteContact(int contactId, CancellationToken cancellationToken)
    {
        var contact = await database.ExposureContacts
            .SingleOrDefaultAsync(candidate => candidate.Id == contactId, cancellationToken);

        if (contact is null)
        {
            return false;
        }

        database.ExposureContacts.Remove(contact);
        await database.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// Corrects a source and answers with it as stored, or null where no source
    /// has that id. A person the source names is validated before this is
    /// called; a source may name none.
    /// </summary>
    public async Task<ExposureSourceResponse?> UpdateSource(
        int sourceId,
        ExposureSourceRequest request,
        CancellationToken cancellationToken)
    {
        var source = await database.ExposureSources
            .SingleOrDefaultAsync(candidate => candidate.Id == sourceId, cancellationToken);

        if (source is null)
        {
            return null;
        }

        source.PersonId = request.PersonId;
        source.PersonConditionId = request.PersonConditionId;
        source.SourceName = request.SourceName;
        source.SourceType = request.SourceType;

        await database.SaveChangesAsync(cancellationToken);

        return ExposureEndpoints.SourceToResponse.Compile().Invoke(source);
    }

    /// <summary>Removes a source — a leaf — and says whether it was there.</summary>
    public async Task<bool> DeleteSource(int sourceId, CancellationToken cancellationToken)
    {
        var source = await database.ExposureSources
            .SingleOrDefaultAsync(candidate => candidate.Id == sourceId, cancellationToken);

        if (source is null)
        {
            return false;
        }

        database.ExposureSources.Remove(source);
        await database.SaveChangesAsync(cancellationToken);

        return true;
    }
}
