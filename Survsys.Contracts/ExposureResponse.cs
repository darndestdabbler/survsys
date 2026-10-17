namespace Survsys.Contracts;

/// <summary>
/// An exposure, as the API tier returns it in a list of a report's exposures:
/// what <c>GET /api/person-conditions/{id}/exposures</c> returns one row of.
/// </summary>
/// <remarks>
/// The exposure's own fields, which are the legacy <c>exposure</c> table's
/// columns as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, as the Contacts tab that
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps lists them. The report, the condition and the agency are not here,
/// because a list is read under the report that scopes it; the place, the
/// contacts and the sources that hang off an exposure are
/// <see cref="ExposureDetailResponse"/>'s, read one exposure at a time. The
/// place is its bare id, so a list shows where an exposure has one.
/// </remarks>
/// <param name="Id">The exposure's identifier, as every other resource refers to it.</param>
/// <param name="Name">What the exposure is called, or null where it has no name.</param>
/// <param name="Description">What happened, or null where nothing was written.</param>
/// <param name="ExposureStart">The first day of the exposure, or null where it is not known.</param>
/// <param name="ExposureEnd">The last day of the exposure, or null where it is not known.</param>
/// <param name="ExposurePlaceId">The place the exposure happened, as the legacy <c>exposure_place_id</c>, or null where none is recorded.</param>
public record ExposureResponse(
    int Id,
    string? Name,
    string? Description,
    DateOnly? ExposureStart,
    DateOnly? ExposureEnd,
    int? ExposurePlaceId);
