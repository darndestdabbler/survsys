namespace Survsys.Contracts;

/// <summary>
/// A test to record on a specimen, with its results: part of what
/// <c>POST /api/person-conditions/{id}/labs</c> takes, nested under its
/// specimen.
/// </summary>
/// <remarks>
/// The writable columns of the legacy <c>lab_test</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, less the identity and the lab id, which the server
/// assigns and the nesting carries, together with the results to record for
/// the test. The test type and the status are the bare legacy ids, because the
/// reference tables behind them do not exist here yet.
///
/// <see cref="Results"/> is left out where a test has no result yet, and binds
/// null; the endpoint reads a missing list as an empty one.
/// </remarks>
/// <param name="TestTypeId">What test this is, as the legacy <c>test_type_id</c>.</param>
/// <param name="TestStatusId">The status of the test, as the legacy <c>test_status_id</c>.</param>
/// <param name="LoincCode">The lab's LOINC code for the test.</param>
/// <param name="ReferenceRange">The reference range the result is read against.</param>
/// <param name="LabTestDate">The date the test was run.</param>
/// <param name="Results">The results to record for this test.</param>
public record CreateLabTestRequest(
    int? TestTypeId = null,
    int? TestStatusId = null,
    string? LoincCode = null,
    string? ReferenceRange = null,
    DateOnly? LabTestDate = null,
    IReadOnlyList<CreateLabTestResultRequest>? Results = null);
