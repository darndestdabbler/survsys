namespace Survsys.Contracts;

/// <summary>
/// The corrections to a stored exposure's own columns: what
/// <c>PUT /api/exposures/{id}</c> takes.
/// </summary>
/// <remarks>
/// The exposure's own columns <see cref="CreateExposureRequest"/> records, less
/// the contacts and sources, which are corrected through their own routes
/// (<c>PUT /api/exposure-contacts/{id}</c> and
/// <c>/api/exposure-sources/{id}</c>): an exposure edit is of the exposure row,
/// not of the rows that hang off it. The place is the one thing hung off an
/// exposure the exposure row itself points at, by
/// <see cref="ExposurePlaceId"/>, so an edit may change which place the
/// exposure points at or clear it — the place's own columns are still corrected
/// through <c>PUT /api/exposure-places/{id}</c>. The report, condition and
/// agency an exposure is scoped to are not here and do not change, as they were
/// never taken on the wire when it was recorded. Every field is nullable, and
/// the save is a whole replacement of the exposure's own columns, a field sent
/// null clearing it.
/// </remarks>
/// <param name="Name">What the exposure is called.</param>
/// <param name="Description">What happened, in words.</param>
/// <param name="ExposureStart">The day the exposure began.</param>
/// <param name="ExposureEnd">The day the exposure ended.</param>
/// <param name="ExposurePlaceId">
/// The id of the place the exposure happened at, or null to point the exposure
/// at no place — which is how a place is detached before it is removed, since a
/// place an exposure still points at cannot be deleted.
/// </param>
public record UpdateExposureRequest(
    string? Name = null,
    string? Description = null,
    DateOnly? ExposureStart = null,
    DateOnly? ExposureEnd = null,
    int? ExposurePlaceId = null);
