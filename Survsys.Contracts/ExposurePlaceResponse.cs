namespace Survsys.Contracts;

/// <summary>
/// The place an exposure happened, as the API tier returns it inside
/// <see cref="ExposureDetailResponse"/>.
/// </summary>
/// <remarks>
/// The legacy <c>exposure_place</c> table's columns, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, the row the Investigation tab's "+ Exposure" panel
/// records with the exposure as
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps it. <see cref="ExposurePlaceRequest"/> with the identifier the server
/// assigned: a recorded place reads back whole. The place type is the bare
/// legacy id and the state a bare code, because the reference tables behind
/// them do not exist here yet.
/// </remarks>
/// <param name="Id">The place's identifier.</param>
/// <param name="ExposurePlaceTypeId">What kind of place this is, as the legacy <c>exposure_place_type_id</c>, or null.</param>
/// <param name="Name">What the place is called, or null.</param>
/// <param name="Street">The street line of the place's address, or null.</param>
/// <param name="City">The city of the place's address, or null.</param>
/// <param name="Zip">The postal code of the place's address, or null.</param>
/// <param name="State">The state of the place's address, as a code, or null.</param>
/// <param name="Latitude">The place's latitude, in degrees, or null.</param>
/// <param name="Longitude">The place's longitude, in degrees, or null.</param>
/// <param name="Phone">The place's phone number, or null.</param>
public record ExposurePlaceResponse(
    int Id,
    int? ExposurePlaceTypeId,
    string? Name,
    string? Street,
    string? City,
    string? Zip,
    string? State,
    decimal? Latitude,
    decimal? Longitude,
    string? Phone);
