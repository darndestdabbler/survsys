namespace Survsys.Contracts;

/// <summary>
/// A test run on a laboratory specimen, with its results, as the API tier
/// returns it inside <see cref="LabDetailResponse"/>.
/// </summary>
/// <remarks>
/// The legacy <c>lab_test</c> table's columns, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, together with the results that hang off the test —
/// so reading a specimen reads its tests and their results in one shape. The
/// test type and the status are the bare legacy ids, because the reference
/// tables behind them do not exist here yet.
///
/// <see cref="Results"/> is <see cref="IReadOnlyList{T}"/>, as a response is
/// read and not added to, and is present but empty where a test has no result
/// yet.
/// </remarks>
/// <param name="Id">The test's identifier.</param>
/// <param name="LabId">The specimen this test was run on.</param>
/// <param name="TestTypeId">
/// What test this is, as the legacy <c>test_type_id</c>, or null where it is
/// not recorded.
/// </param>
/// <param name="TestStatusId">
/// The status of the test, as the legacy <c>test_status_id</c>, or null.
/// </param>
/// <param name="LoincCode">The lab's LOINC code for the test, or null.</param>
/// <param name="ReferenceRange">
/// The reference range the result is read against, or null.
/// </param>
/// <param name="LabTestDate">The date the test was run, or null where it is not known.</param>
/// <param name="Results">The results of this test, in the order the API returned them.</param>
public record LabTestResponse(
    int Id,
    int LabId,
    int? TestTypeId,
    int? TestStatusId,
    string? LoincCode,
    string? ReferenceRange,
    DateOnly? LabTestDate,
    IReadOnlyList<LabTestResultResponse> Results);
