namespace Survsys.Contracts;

/// <summary>
/// One exposure whole: the exposure, the place it happened, the contacts who
/// were there and the sources it came from — the shape
/// <c>GET /api/exposures/{id}</c> returns, and what recording one answers
/// with.
/// </summary>
/// <remarks>
/// The legacy <c>exposure</c> table's columns, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, together with the rows of <c>exposure_place</c>,
/// <c>exposure_contact</c> and <c>exposure_source</c> that hang off it, as
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps the Investigation tab's "+ Exposure" panel and the Contacts tab. Where
/// <see cref="ExposureResponse"/> is what a list has use for, this is what a
/// reader of one exposure does: the report, the condition and the agency the
/// server set from the route, the place as a row rather than an id, and the
/// contacts and sources. The condition and the agency are the bare legacy ids,
/// because the reference tables behind them do not exist here yet.
///
/// <see cref="Place"/> is null where the exposure has no place. Both
/// collections are <see cref="IReadOnlyList{T}"/>, as a response is read and
/// not added to, and are present but empty where an exposure has no contacts
/// or no sources.
/// </remarks>
/// <param name="Id">The exposure's identifier, as every other resource refers to it.</param>
/// <param name="PersonConditionId">The case morbidity report the exposure was recorded against, or null where none is set.</param>
/// <param name="ConditionId">The disease the exposure is to, as the legacy <c>condition_id</c>, or null.</param>
/// <param name="AgencyId">The agency that recorded the exposure, as the legacy <c>agency_id</c>, or null.</param>
/// <param name="Name">What the exposure is called, or null where it has no name.</param>
/// <param name="Description">What happened, or null where nothing was written.</param>
/// <param name="ExposureStart">The first day of the exposure, or null where it is not known.</param>
/// <param name="ExposureEnd">The last day of the exposure, or null where it is not known.</param>
/// <param name="Place">The place the exposure happened, or null where none is recorded.</param>
/// <param name="Contacts">The contacts recorded as having been at the exposure, in the order the API returned them.</param>
/// <param name="Sources">The sources the exposure is recorded as coming from, in the order the API returned them.</param>
public record ExposureDetailResponse(
    int Id,
    int? PersonConditionId,
    int? ConditionId,
    int? AgencyId,
    string? Name,
    string? Description,
    DateOnly? ExposureStart,
    DateOnly? ExposureEnd,
    ExposurePlaceResponse? Place,
    IReadOnlyList<ExposureContactResponse> Contacts,
    IReadOnlyList<ExposureSourceResponse> Sources);
