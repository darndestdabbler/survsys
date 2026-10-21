using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>POST /api/person-conditions/{id}/contacts/import</c>: parses and
/// validates a batch CSV of contacts against a report, saves every row it
/// accepts as a contact event under the exposure the batch names, and answers
/// with the rows it saved and the rows it refused.
/// </summary>
/// <remarks>
/// The legacy Contacts tab's "Import" button, as
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// lists it, writes the batch to the <c>contact_import_batch</c>,
/// <c>contact_import_row</c> and <c>contact_import_answer</c> tables. None of
/// those is modelled here: the parsing is <see cref="ContactImportParser"/>'s,
/// pure, and what is kept is what the import is for — each accepted row becomes
/// a new person, made by <see cref="ToPerson"/>, and a contact event of theirs
/// under the exposure, written by <see cref="ContactEventQueries"/> as a
/// contact added by hand is, all in one transaction. A refused row writes
/// nothing and comes back with its line and reason, so the file can be
/// corrected and the refused rows posted again. No row is matched against a
/// person already on file; each is a new person.
///
/// The handler is handed the queries classes and not the <c>DbContext</c>,
/// because the architecture test lets no <c>Survsys.Data</c> type into an
/// endpoint signature at all. The answer is a 200 and not a 201, because it
/// describes the batch rather than one resource created.
/// </remarks>
public static class ContactImportEndpoints
{
    /// <summary>
    /// The new person an accepted row describes: the names, the birth date, the
    /// sex as the birth gender, and an address, a telephone and an email where
    /// the row gives one.
    /// </summary>
    /// <remarks>
    /// The parser has trimmed every field and made an empty one null, so the
    /// row is carried as it reads. The address is one row of the person's
    /// addresses when any of its parts is given, and none otherwise. Public
    /// because the unit suite proves it.
    /// </remarks>
    public static Person ToPerson(ImportedContact contact)
    {
        ArgumentNullException.ThrowIfNull(contact);

        var person = new Person
        {
            LastName = contact.LastName,
            FirstName = contact.FirstName,
            MiddleName = contact.MiddleName,
            BirthDate = contact.BirthDate,
            BirthGender = contact.Sex,
        };

        if (contact.Street is not null || contact.Unit is not null || contact.City is not null
            || contact.State is not null || contact.Zip is not null || contact.County is not null)
        {
            person.Addresses.Add(new PersonAddress
            {
                Street = contact.Street,
                UnitNumber = contact.Unit,
                City = contact.City,
                State = contact.State,
                PostalCode = contact.Zip,
                County = contact.County,
            });
        }

        if (contact.Phone is not null)
        {
            person.Telephones.Add(new PersonTelephone { PhoneNumber = contact.Phone });
        }

        if (contact.Email is not null)
        {
            person.Emails.Add(new PersonEmail { EmailAddress = contact.Email });
        }

        return person;
    }

    /// <summary>
    /// Registers what the contact import needs of the container. Kept here
    /// with the endpoint so the slice's wiring is in one place.
    /// </summary>
    public static IServiceCollection AddContactImport(this IServiceCollection services) =>
        services.AddScoped<ContactImportQueries>();

    public static IEndpointRouteBuilder MapContactImport(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/person-conditions/{id:int}/contacts/import", ImportContacts)
            .WithName("ImportContacts")
            .WithSummary("Imports a batch CSV of contacts as contact events under one of a "
                + "case morbidity report's exposures.");

        return routes;
    }

    /// <remarks>
    /// <c>DateTimeOffset.Now</c> is read here, local and not UTC, for the
    /// reason the report creation reads it so: the record numbers carry the
    /// year the events were created in.
    /// </remarks>
    private static async Task<Results<Ok<ContactImportResponse>, ProblemHttpResult>> ImportContacts(
        ContactImportQueries reports,
        ContactEventQueries contactEvents,
        int id,
        ImportContactsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await reports.HasReport(id, cancellationToken))
        {
            return TypedResults.Problem(
                detail: $"No case morbidity report has id {id}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such case morbidity report.");
        }

        if (request.ExposureId is not { } exposureId || exposureId <= 0)
        {
            return Refused("exposureId is required: a batch of contacts is imported under one "
                + "of the report's exposures.");
        }

        if (!await reports.HasExposure(id, exposureId, cancellationToken))
        {
            return Refused($"exposureId {exposureId} is not an exposure of report {id}.");
        }

        var parsed = ContactImportParser.Parse(request.Csv);

        if (parsed.Accepted.Count == 0)
        {
            return TypedResults.Ok(parsed);
        }

        var added = await contactEvents.Add(
            exposureId,
            parsed.Accepted
                .Select(contact => new ContactToAdd(null, ToPerson(contact), null, null, null))
                .ToList(),
            DateTimeOffset.Now,
            cancellationToken);

        return TypedResults.Ok(parsed with
        {
            Accepted = parsed.Accepted
                .Zip(added, (contact, row) => contact with { PersonConditionId = row.PersonConditionId })
                .ToList(),
        });
    }

    private static ProblemHttpResult Refused(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The batch asked for is not one this endpoint can import.");
}

/// <summary>
/// The two questions the contact import asks the database before it writes.
/// </summary>
/// <remarks>
/// Internal, and in this file rather than its own, for the reason
/// <c>LabQueries</c> is: it exists only to serve
/// <see cref="ContactImportEndpoints"/>, and it is the named step between an
/// endpoint signature and <c>Survsys.Data</c> that the architecture test
/// requires. It reads and never writes; the writes are
/// <see cref="ContactEventQueries"/>'s.
/// </remarks>
internal sealed class ContactImportQueries(SurvsysDbContext database)
{
    /// <summary>
    /// Whether a report has that id — false is the answer the endpoint turns
    /// into a 404.
    /// </summary>
    public Task<bool> HasReport(int reportId, CancellationToken cancellationToken) =>
        database.PersonConditions.AnyAsync(report => report.Id == reportId, cancellationToken);

    /// <summary>Whether the exposure is one recorded against the report.</summary>
    public Task<bool> HasExposure(int reportId, int exposureId, CancellationToken cancellationToken) =>
        database.Exposures.AnyAsync(
            exposure => exposure.Id == exposureId && exposure.PersonConditionId == reportId,
            cancellationToken);
}
