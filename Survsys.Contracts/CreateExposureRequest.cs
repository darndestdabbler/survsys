namespace Survsys.Contracts;

/// <summary>
/// An exposure event to record against a case morbidity report, with the
/// place it happened, the contacts who were there and the sources it came
/// from: what <c>POST /api/person-conditions/{id}/exposures</c> takes.
/// </summary>
/// <remarks>
/// The writable columns of the legacy <c>exposure</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, less the identity the server assigns, as the
/// Investigation tab's "+ Exposure" panel records one and the Contacts tab
/// lists its contacts —
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps both.
///
/// <strong>The report, the condition and the agency are not here.</strong> An
/// exposure is recorded against the report in the route, so the server sets
/// <c>person_condition_id</c>, <c>condition_id</c> and <c>agency_id</c> from
/// that report rather than taking them on the wire, as the laboratory slice
/// sets a specimen's person: a caller that could name a report could name one
/// the exposure is not about. The place is not an id either, because the
/// <c>exposure_place</c> row is recorded with the exposure that points at it
/// and the server assigns its identity.
///
/// <see cref="Place"/> is left out where the exposure has no place, and
/// <see cref="Contacts"/> and <see cref="Sources"/> where it has none; each
/// binds null, and the endpoint reads a missing list as an empty one.
/// </remarks>
/// <param name="Name">What the exposure is called.</param>
/// <param name="Description">What happened.</param>
/// <param name="ExposureStart">The first day of the exposure.</param>
/// <param name="ExposureEnd">The last day of the exposure.</param>
/// <param name="Place">The place the exposure happened, recorded with it.</param>
/// <param name="Contacts">The contacts to record as having been at the exposure.</param>
/// <param name="Sources">The sources to record the exposure as coming from.</param>
public record CreateExposureRequest(
    string? Name = null,
    string? Description = null,
    DateOnly? ExposureStart = null,
    DateOnly? ExposureEnd = null,
    ExposurePlaceRequest? Place = null,
    IReadOnlyList<ExposureContactRequest>? Contacts = null,
    IReadOnlyList<ExposureSourceRequest>? Sources = null);
