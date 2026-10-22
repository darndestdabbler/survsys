namespace Survsys.Contracts;

/// <summary>
/// The corrections to a stored laboratory specimen's own columns: what
/// <c>PUT /api/labs/{id}</c> takes.
/// </summary>
/// <remarks>
/// The writable columns <see cref="CreateLabRequest"/> records, less the tests
/// and susceptibility, which are corrected through their own routes
/// (<c>PUT /api/lab-tests/{id}</c>, <c>/api/lab-test-results/{id}</c> and
/// <c>/api/lab-resist-tests/{id}</c>): a specimen edit is of the specimen row,
/// not of what hangs off it. The person and the report a specimen belongs to
/// are not here and do not change, as they were never taken on the wire when
/// the specimen was recorded; every field is nullable, and the save is a whole
/// replacement of the specimen's own columns, a field sent null clearing it.
/// </remarks>
/// <param name="LabFacilityId">
/// The facility that performed the lab work, as the legacy
/// <c>lab_facility_id</c>.
/// </param>
/// <param name="ProviderId">
/// The clinician who ordered the lab work, as the legacy <c>provider_id</c>.
/// </param>
/// <param name="DiagnosticFacilityId">
/// The facility that performed the diagnosis, as the legacy
/// <c>diagnostic_facility_id</c>.
/// </param>
/// <param name="ReportingFacilityId">
/// The facility that reported the result, as the legacy
/// <c>reporting_facility_id</c>.
/// </param>
/// <param name="SpecimenSourceId">
/// What the specimen is, as the legacy <c>specimen_source_id</c>.
/// </param>
/// <param name="CollectionDate">The date the specimen was collected.</param>
/// <param name="AccessionNo">
/// The accession number the performing lab knows the specimen by.
/// </param>
/// <param name="SpecimenSentToState">Whether the specimen was sent on to the state lab.</param>
public record UpdateLabRequest(
    int? LabFacilityId = null,
    int? ProviderId = null,
    int? DiagnosticFacilityId = null,
    int? ReportingFacilityId = null,
    int? SpecimenSourceId = null,
    DateOnly? CollectionDate = null,
    string? AccessionNo = null,
    bool? SpecimenSentToState = null);
