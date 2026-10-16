namespace Survsys.Contracts;

/// <summary>
/// A laboratory specimen, as the API tier returns it in a list.
/// </summary>
/// <remarks>
/// The specimen's own fields, which are the legacy <c>lab</c> table's columns
/// as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>. A list of a report's specimens shows these; the
/// tests and susceptibility rows that hang off a specimen are
/// <see cref="LabDetailResponse"/>'s, read one specimen at a time.
///
/// The facility and the specimen source are the bare legacy ids, because the
/// reference tables behind them do not exist here yet; a list shows the ids
/// and the screen resolves their names when those tables arrive.
/// </remarks>
/// <param name="Id">The specimen's identifier, as every other resource refers to it.</param>
/// <param name="PersonId">The person the specimen was collected from.</param>
/// <param name="LabFacilityId">
/// The facility that performed the lab work, as the legacy
/// <c>lab_facility_id</c>, or null where none is recorded.
/// </param>
/// <param name="ProviderId">
/// The clinician who ordered the lab work, as the legacy <c>provider_id</c>,
/// or null where none is recorded.
/// </param>
/// <param name="DiagnosticFacilityId">
/// The facility that performed the diagnosis, as the legacy
/// <c>diagnostic_facility_id</c>, or null where none is recorded.
/// </param>
/// <param name="ReportingFacilityId">
/// The facility that reported the result, as the legacy
/// <c>reporting_facility_id</c>, or null where none is recorded.
/// </param>
/// <param name="SpecimenSourceId">
/// What the specimen is, as the legacy <c>specimen_source_id</c>, or null
/// where it is not recorded.
/// </param>
/// <param name="CollectionDate">
/// The date the specimen was collected, or null where it is not known.
/// </param>
/// <param name="AccessionNo">
/// The accession number the performing lab knows the specimen by, or null
/// where it has none.
/// </param>
/// <param name="SpecimenSentToState">
/// Whether the specimen was sent on to the state lab, or null where it is not
/// recorded.
/// </param>
public record LabResponse(
    int Id,
    int PersonId,
    int? LabFacilityId,
    int? ProviderId,
    int? DiagnosticFacilityId,
    int? ReportingFacilityId,
    int? SpecimenSourceId,
    DateOnly? CollectionDate,
    string? AccessionNo,
    bool? SpecimenSentToState);
