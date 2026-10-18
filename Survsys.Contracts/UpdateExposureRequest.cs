namespace Survsys.Contracts;

/// <summary>
/// The corrections to a stored exposure's own columns: what
/// <c>PUT /api/exposures/{id}</c> takes.
/// </summary>
/// <remarks>
/// The exposure's own columns <see cref="CreateExposureRequest"/> records, less
/// the place, contacts and sources, which are corrected through their own
/// routes (<c>PUT /api/exposure-places/{id}</c>,
/// <c>/api/exposure-contacts/{id}</c> and <c>/api/exposure-sources/{id}</c>):
/// an exposure edit is of the exposure row, not of what hangs off it. The
/// report, condition and agency an exposure is scoped to are not here and do
/// not change, as they were never taken on the wire when it was recorded.
/// Every field is nullable, and the save is a whole replacement of the
/// exposure's own columns, a field sent null clearing it.
/// </remarks>
/// <param name="Name">What the exposure is called.</param>
/// <param name="Description">What happened, in words.</param>
/// <param name="ExposureStart">The day the exposure began.</param>
/// <param name="ExposureEnd">The day the exposure ended.</param>
public record UpdateExposureRequest(
    string? Name = null,
    string? Description = null,
    DateOnly? ExposureStart = null,
    DateOnly? ExposureEnd = null);
