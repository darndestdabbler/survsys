namespace Survsys.Contracts;

/// <summary>
/// The place an exposure happened, as it is sent to be recorded with the
/// exposure: part of what <c>POST /api/person-conditions/{id}/exposures</c>
/// takes, nested under <see cref="CreateExposureRequest"/>.
/// </summary>
/// <remarks>
/// The writable columns of the legacy <c>exposure_place</c> table, as declared
/// in <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, less the identity the server assigns; the screen is
/// the Investigation tab's "+ Exposure" panel that
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps to <c>exposure</c> and its related <c>exposure_place</c>. Every field
/// is nullable: a place may be no more than a name. The place type is the
/// bare legacy id and the state a bare code, because the reference tables
/// behind them do not exist here yet.
/// </remarks>
/// <param name="ExposurePlaceTypeId">What kind of place this is, as the legacy <c>exposure_place_type_id</c>.</param>
/// <param name="Name">What the place is called.</param>
/// <param name="Street">The street line of the place's address.</param>
/// <param name="City">The city of the place's address.</param>
/// <param name="Zip">The postal code of the place's address.</param>
/// <param name="State">The state of the place's address, as a code.</param>
/// <param name="Latitude">The place's latitude, in degrees.</param>
/// <param name="Longitude">The place's longitude, in degrees.</param>
/// <param name="Phone">The place's phone number.</param>
public record ExposurePlaceRequest(
    int? ExposurePlaceTypeId = null,
    string? Name = null,
    string? Street = null,
    string? City = null,
    string? Zip = null,
    string? State = null,
    decimal? Latitude = null,
    decimal? Longitude = null,
    string? Phone = null);
