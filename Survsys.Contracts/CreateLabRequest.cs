namespace Survsys.Contracts;

/// <summary>
/// A laboratory specimen to record against a case morbidity report, with its
/// tests and susceptibility: what <c>POST /api/person-conditions/{id}/labs</c>
/// takes.
/// </summary>
/// <remarks>
/// The writable columns of the legacy <c>lab</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, less the identity the server assigns.
///
/// <strong>The person is not here.</strong> A specimen is collected from the
/// person the report is about, so the server reads the person from the report
/// in the route rather than taking one on the wire: a caller that could name a
/// person could name one the report is not about. The facility and the
/// specimen source are the bare legacy ids, because the reference tables
/// behind them do not exist here yet.
///
/// <see cref="Tests"/> and <see cref="ResistTests"/> are left out where a
/// specimen has none, and bind null; the endpoint reads a missing list as an
/// empty one.
/// </remarks>
/// <param name="LabFacilityId">
/// The facility that performed the lab work, as the legacy
/// <c>lab_facility_id</c>.
/// </param>
/// <param name="SpecimenSourceId">
/// What the specimen is, as the legacy <c>specimen_source_id</c>.
/// </param>
/// <param name="CollectionDate">The date the specimen was collected.</param>
/// <param name="AccessionNo">
/// The accession number the performing lab knows the specimen by.
/// </param>
/// <param name="SpecimenSentToState">Whether the specimen was sent on to the state lab.</param>
/// <param name="Tests">The tests to record on the specimen, each with its results.</param>
/// <param name="ResistTests">
/// The antimicrobial susceptibility tests to record on the specimen.
/// </param>
public record CreateLabRequest(
    int? LabFacilityId = null,
    int? SpecimenSourceId = null,
    DateOnly? CollectionDate = null,
    string? AccessionNo = null,
    bool? SpecimenSentToState = null,
    IReadOnlyList<CreateLabTestRequest>? Tests = null,
    IReadOnlyList<CreateLabResistTestRequest>? ResistTests = null);
