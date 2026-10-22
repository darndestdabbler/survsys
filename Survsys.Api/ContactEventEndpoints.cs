using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>POST /api/exposures/{id}/contact-events</c>: a contact added to
/// an exposure as a contact event of its own.
/// </summary>
/// <remarks>
/// The legacy Contacts tab, as
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// records it, adds a contact by finding the person or creating one, and the
/// contact can then have a case of their own. Here the two happen together: the
/// contact row records the person against the exposure, and a
/// <see cref="EventTypes.Contact"/> event — a row of the reports table, with
/// the index case's condition and agency and a record number of its own — is
/// opened for them and named by the row. Promoting that event to a morbidity
/// report is <c>POST /api/person-conditions/{id}/promote</c>'s.
///
/// The handler is handed <see cref="ContactEventQueries"/> and not the
/// <c>DbContext</c>, because the architecture test lets no <c>Survsys.Data</c>
/// type into an endpoint signature.
/// </remarks>
public static class ContactEventEndpoints
{
    /// <summary>
    /// Why this is not a contact this endpoint can add, as far as can be said
    /// without the database, or null when nothing is wrong with it.
    /// </summary>
    /// <remarks>
    /// The person is named exactly one way: an existing person by
    /// <c>personId</c>, as the search found them, or a new one by
    /// <c>newPerson</c>, which is held to the rule
    /// <see cref="PersonEndpoints.WhyNotAPerson(CreatePersonRequest)"/> holds
    /// every new person to. A contact's dates are not compared here, as an
    /// exposure contact's are not compared on its edit. Public because the
    /// unit suite proves it.
    /// </remarks>
    public static string? WhyNotAContactEvent(CreateContactEventRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PersonId is null == request.NewPerson is null)
        {
            return "Name the contact exactly one way: personId for a person already on file, "
                + "as GET /api/persons finds them, or newPerson for one to create.";
        }

        if (request.PersonId is { } personId && personId <= 0)
        {
            return $"personId {personId} is not an identifier anyone has.";
        }

        if (request.NewPerson is { } newPerson && PersonEndpoints.WhyNotAPerson(newPerson) is { } why)
        {
            return $"newPerson: {why}";
        }

        return null;
    }

    /// <summary>
    /// Registers what the contact events need of the container. Kept here with
    /// the endpoint so the slice's wiring is in one place.
    /// </summary>
    public static IServiceCollection AddContactEvents(this IServiceCollection services) =>
        services.AddScoped<ContactEventQueries>();

    public static IEndpointRouteBuilder MapContactEvents(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/exposures/{id:int}/contact-events", AddContactEvent)
            .WithName("AddContactEvent")
            .WithSummary("Adds a contact to an exposure as a contact event of their own.");

        return routes;
    }

    /// <remarks>
    /// <c>DateTimeOffset.Now</c> is read here, local and not UTC, for the
    /// reason the report creation reads it so: the record number carries the
    /// year the event was created in.
    /// </remarks>
    private static async Task<Results<Created<ExposureContactResponse>, ProblemHttpResult>> AddContactEvent(
        ContactEventQueries contacts,
        int id,
        CreateContactEventRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotAContactEvent(request) is { } why)
        {
            return Refused(why);
        }

        if (!await contacts.HasExposure(id, cancellationToken))
        {
            return TypedResults.Problem(
                detail: $"No exposure has id {id}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such exposure.");
        }

        if (request.PersonId is { } personId && !await contacts.PersonOnFile(personId, cancellationToken))
        {
            return Refused($"personId {personId} is not a person on file: find the person "
                + "first, with GET /api/persons, or create them with newPerson.");
        }

        var contact = new ContactToAdd(
            request.PersonId,
            request.NewPerson is { } newPerson ? PersonEndpoints.ToPerson(newPerson) : null,
            request.ContactType,
            request.ExposureStart,
            request.ExposureEnd);

        var added = (await contacts.Add(id, [contact], DateTimeOffset.Now, cancellationToken)).Single();

        return TypedResults.Created($"/api/exposures/{id}", added);
    }

    private static ProblemHttpResult Refused(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The contact asked for is not one this endpoint can add.");
}

/// <summary>
/// One contact to add to an exposure: the person, either on file by id or new,
/// and what the contact row records of them.
/// </summary>
/// <remarks>
/// Exactly one of <see cref="PersonId"/> and <see cref="NewPerson"/> is set;
/// the callers validate that before building one.
/// </remarks>
internal sealed record ContactToAdd(
    int? PersonId,
    Person? NewPerson,
    string? ContactType,
    DateOnly? ExposureStart,
    DateOnly? ExposureEnd);

/// <summary>
/// The contact event writes, and the only thing in this tier that holds the
/// <c>DbContext</c> for them — shared by the contact-event route and the batch
/// contact import.
/// </summary>
/// <remarks>
/// Internal, and in this file rather than its own, for the reason
/// <c>ExposureQueries</c> is: it is the named step between an endpoint signature and
/// <c>Survsys.Data</c> that the architecture test requires.
/// </remarks>
internal sealed class ContactEventQueries(SurvsysDbContext database)
{
    /// <summary>Whether an exposure has that id and belongs to a report.</summary>
    public Task<bool> HasExposure(int exposureId, CancellationToken cancellationToken) =>
        database.Exposures.AnyAsync(
            exposure => exposure.Id == exposureId && exposure.PersonConditionId != null,
            cancellationToken);

    /// <summary>Whether a person has that id.</summary>
    public Task<bool> PersonOnFile(int personId, CancellationToken cancellationToken) =>
        database.Persons.AnyAsync(person => person.Id == personId, cancellationToken);

    /// <summary>
    /// Adds each contact to the exposure as a contact event of their own, and
    /// answers with the contact rows as stored, in the order given.
    /// </summary>
    /// <remarks>
    /// The event takes the index case's condition and agency, because a
    /// contact is exposed to that case's condition; its onset date, MMWR week
    /// and year and initial workflow state are set as a new report's are. It
    /// is not queued for notification and is given no forms, because it is not
    /// a case until it is promoted.
    ///
    /// The record numbers follow the year's highest in sequence, read once, so
    /// a batch takes consecutive numbers; the unique index refuses the batch if
    /// a report is created between the read and the save, as it refuses a
    /// single creation. The keys are database-generated and the entities have
    /// no navigations, so the writes are saved in dependency order inside one
    /// transaction, as an exposure's are: the new people and the events first,
    /// then the contact rows that name them.
    /// </remarks>
    public async Task<IReadOnlyList<ExposureContactResponse>> Add(
        int exposureId,
        IReadOnlyList<ContactToAdd> contacts,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        var index = await database.Exposures
            .Where(exposure => exposure.Id == exposureId)
            .Join(database.PersonConditions,
                exposure => exposure.PersonConditionId,
                report => report.Id,
                (exposure, report) => new { report.ConditionId, report.AgencyId })
            .SingleAsync(cancellationToken);

        var year = createdAt.Year;
        var issued = year.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
        var highest = await database.PersonConditions
            .Where(report => report.RecordNumber.StartsWith(issued))
            .OrderByDescending(report => report.RecordNumber)
            .Select(report => report.RecordNumber)
            .FirstOrDefaultAsync(cancellationToken);
        var sequence = RecordNumber.SequenceIn(highest, year) ?? 0;

        var onset = DateOnly.FromDateTime(createdAt.Date);
        var mmwr = MmwrWeek.Of(onset);

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        foreach (var contact in contacts)
        {
            if (contact.NewPerson is { } person)
            {
                database.Persons.Add(person);
            }
        }

        await database.SaveChangesAsync(cancellationToken);

        var events = contacts
            .Select(contact => new PersonCondition
            {
                PersonId = contact.PersonId ?? contact.NewPerson!.Id,
                ConditionId = index.ConditionId,
                RecordNumber = RecordNumber.For(year, ++sequence),
                AgencyId = index.AgencyId,
                EventOnsetDate = onset,
                CreatedAt = createdAt,
                MmwrWeek = mmwr.Week,
                MmwrYear = mmwr.Year,
                WorkflowState = WorkflowRules.InitialCode,
                EventType = EventTypes.Contact,
            })
            .ToList();

        database.PersonConditions.AddRange(events);
        await database.SaveChangesAsync(cancellationToken);

        var rows = contacts
            .Zip(events, (contact, contactEvent) => new ExposureContact
            {
                ExposureId = exposureId,
                PersonId = contactEvent.PersonId,
                ExposureStart = contact.ExposureStart,
                ExposureEnd = contact.ExposureEnd,
                ContactType = contact.ContactType,
                PersonConditionId = contactEvent.Id,
            })
            .ToList();

        database.ExposureContacts.AddRange(rows);
        await database.SaveChangesAsync(cancellationToken);

        var classifier = new CaseClassifier(database);
        foreach (var contactEvent in events)
        {
            await classifier.ClassifyAsync(contactEvent.Id, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        var respond = ExposureEndpoints.ContactToResponse.Compile();

        return rows.Select(respond).ToList();
    }
}
