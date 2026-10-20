namespace Survsys.Contracts;

/// <summary>
/// A contact to add to an exposure as a contact event of their own: what
/// <c>POST /api/exposures/{id}/contact-events</c> takes.
/// </summary>
/// <remarks>
/// The legacy Contacts tab, as
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// records it, finds the contact's person by search or creates them with
/// "+ New". So the person is named exactly one way: <see cref="PersonId"/> for
/// one the search found, or <see cref="NewPerson"/> for one to create. The
/// rest is what the contact row records of them; the disposition is set later
/// through the contact's own edit, as it is after an exposure is recorded.
/// </remarks>
/// <param name="PersonId">The person on file who is the contact.</param>
/// <param name="NewPerson">The person to create as the contact.</param>
/// <param name="ContactType">What kind of contact this was, as a code.</param>
/// <param name="ExposureStart">The first day the contact was at the exposure.</param>
/// <param name="ExposureEnd">The last day the contact was at the exposure.</param>
public record CreateContactEventRequest(
    int? PersonId = null,
    CreatePersonRequest? NewPerson = null,
    string? ContactType = null,
    DateOnly? ExposureStart = null,
    DateOnly? ExposureEnd = null);
