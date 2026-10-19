namespace Survsys.Contracts;

/// <summary>
/// A contact of an exposure, as the API tier returns it inside
/// <see cref="ExposureDetailResponse"/>.
/// </summary>
/// <remarks>
/// The legacy <c>exposure_contact</c> table's columns, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>: the row that links a contact's person record to an
/// exposure, carrying the type, disposition and disposition date the Contacts
/// tab shows beside the person, as
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps it. <see cref="ExposureContactRequest"/> with the identifier and the
/// exposure the server assigned: a recorded contact reads back whole. The
/// disposition is the bare legacy id and the contact type a bare code,
/// because the reference tables behind them do not exist here yet.
/// </remarks>
/// <param name="Id">The contact row's identifier.</param>
/// <param name="ExposureId">The exposure this contact was at.</param>
/// <param name="PersonId">The person who was the contact.</param>
/// <param name="ExposureStart">The first day the contact was at the exposure, or null where it is not known.</param>
/// <param name="ExposureEnd">The last day the contact was at the exposure, or null where it is not known.</param>
/// <param name="DispositionId">What the investigation decided about the contact, as the legacy <c>disposition_id</c>, or null.</param>
/// <param name="ContactType">What kind of contact this was, as a code, or null.</param>
/// <param name="DispositionDate">The day the disposition was reached, or null where it is not known.</param>
/// <param name="PersonConditionId">
/// The contact event opened for this contact, or null for a contact recorded
/// with its exposure and given none.
/// </param>
public record ExposureContactResponse(
    int Id,
    int ExposureId,
    int PersonId,
    DateOnly? ExposureStart,
    DateOnly? ExposureEnd,
    int? DispositionId,
    string? ContactType,
    DateOnly? DispositionDate,
    int? PersonConditionId = null);
