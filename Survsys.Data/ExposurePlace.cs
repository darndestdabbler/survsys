namespace Survsys.Data;

/// <summary>
/// The place an exposure happened: a named location with an address, a
/// coordinate and a phone number, recorded with the exposure that points at it.
/// </summary>
/// <remarks>
/// The columns are EpiTrax's own <c>exposure_place</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, down to their snake_case names, and the screen is
/// the Investigation tab's "+ Exposure" panel that
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps to <c>exposure</c> and its related <c>exposure_place</c>.
///
/// The link runs from the exposure to the place and not back: an
/// <see cref="Exposure"/> names its place by <see cref="Exposure.ExposurePlaceId"/>,
/// and a place knows nothing of the exposures recorded at it. The place type
/// is a bare nullable id into the <c>exposure_place_type</c> reference table,
/// which does not exist here yet, exactly as <see cref="Lab.SpecimenSourceId"/>
/// is; the state is a plain code string. There is no behaviour here and no
/// navigation out.
/// </remarks>
public class ExposurePlace
{
    public int Id { get; set; }

    /// <summary>
    /// What kind of place this is, as the legacy <c>exposure_place_type_id</c>.
    /// A bare nullable id into the <c>exposure_place_type</c> reference table,
    /// which does not exist here yet.
    /// </summary>
    public int? ExposurePlaceTypeId { get; set; }

    /// <summary>What the place is called, where it has a name.</summary>
    public string? Name { get; set; }

    /// <summary>The street line of the place's address.</summary>
    public string? Street { get; set; }

    /// <summary>The city of the place's address.</summary>
    public string? City { get; set; }

    /// <summary>The postal code of the place's address, as the legacy <c>zip</c>.</summary>
    public string? Zip { get; set; }

    /// <summary>
    /// The state of the place's address, as a code — the legacy column is a
    /// plain <c>VARCHAR</c>, and the value is the code the dropdown writes.
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// The place's latitude, where it is known.
    /// </summary>
    /// <remarks>
    /// A <see cref="decimal"/> for the legacy <c>NUMERIC</c>; the precision is
    /// set by the configuration, because the legacy declaration of nine digits
    /// and no scale would hold a whole number and nothing else.
    /// </remarks>
    public decimal? Latitude { get; set; }

    /// <summary>
    /// The place's longitude, where it is known.
    /// </summary>
    /// <remarks>
    /// A <see cref="decimal"/> for the legacy <c>NUMERIC</c>, for the reason
    /// <see cref="Latitude"/> is one.
    /// </remarks>
    public decimal? Longitude { get; set; }

    /// <summary>The place's phone number, where it has one.</summary>
    public string? Phone { get; set; }
}
