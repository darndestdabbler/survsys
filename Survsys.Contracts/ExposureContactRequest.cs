namespace Survsys.Contracts;

/// <summary>
/// A contact to record as having been at an exposure: part of what
/// <c>POST /api/person-conditions/{id}/exposures</c> takes, nested under
/// <see cref="CreateExposureRequest"/>.
/// </summary>
/// <remarks>
/// The writable columns of the legacy <c>exposure_contact</c> table, as
/// declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, less the identity and the exposure id, which the
/// server assigns and the nesting carries. The Contacts tab that
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps links a contact to an exposure as a full person record, so
/// <see cref="PersonId"/> names a person that already exists: no contact is
/// created as a person here. The disposition is set with the contact and not
/// changed through an endpoint of its own; it is the bare legacy id and the
/// contact type a bare code, because the reference tables behind them do not
/// exist here yet.
///
/// Every field is nullable on the wire, so a missing person is refused by the
/// endpoint's validation with a reason rather than by binding.
/// </remarks>
/// <param name="PersonId">The existing person the contact is.</param>
/// <param name="ExposureStart">The first day the contact was at the exposure.</param>
/// <param name="ExposureEnd">The last day the contact was at the exposure.</param>
/// <param name="DispositionId">What the investigation decided about the contact, as the legacy <c>disposition_id</c>.</param>
/// <param name="ContactType">What kind of contact this was, as a code.</param>
/// <param name="DispositionDate">The day the disposition was reached.</param>
public record ExposureContactRequest(
    int? PersonId = null,
    DateOnly? ExposureStart = null,
    DateOnly? ExposureEnd = null,
    int? DispositionId = null,
    string? ContactType = null,
    DateOnly? DispositionDate = null);
