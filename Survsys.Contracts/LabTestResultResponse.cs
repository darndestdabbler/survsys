namespace Survsys.Contracts;

/// <summary>
/// The result of a test run on a laboratory specimen, as the API tier returns
/// it.
/// </summary>
/// <remarks>
/// The legacy <c>lab_test_result</c> table's columns, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>. The organism is the bare legacy id, because the
/// <c>organism</c> reference table does not exist here yet.
/// </remarks>
/// <param name="Id">The result's identifier.</param>
/// <param name="LabTestId">The test this is a result of.</param>
/// <param name="OrganismId">
/// The organism the result identifies, as the legacy <c>organism_id</c>, or
/// null where none is named.
/// </param>
/// <param name="ResultValue">The measured value, as the lab reports it, or null.</param>
/// <param name="TestResult">
/// The coded result — Positive/Reactive and the rest — or null where none is
/// recorded.
/// </param>
/// <param name="Comment">The lab's free-text comment on the result, or null.</param>
/// <param name="Units">The units the value is in, or null.</param>
/// <param name="PrimaryLabResult">
/// Whether this is the primary result of its test — the one a summary shows.
/// </param>
public record LabTestResultResponse(
    int Id,
    int LabTestId,
    int? OrganismId,
    string? ResultValue,
    string? TestResult,
    string? Comment,
    string? Units,
    bool PrimaryLabResult);
