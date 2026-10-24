namespace Survsys.Contracts;

/// <summary>
/// A result to record for a test: part of what
/// <c>POST /api/person-conditions/{id}/labs</c> takes, nested under its test.
/// </summary>
/// <remarks>
/// The writable columns of the legacy <c>lab_test_result</c> table, as
/// declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, less the identity and the test id, which the server
/// assigns and the nesting carries. The organism is the bare legacy id,
/// because the <c>organism</c> reference table does not exist here yet.
/// </remarks>
/// <param name="OrganismId">The organism the result identifies, where one is named.</param>
/// <param name="ResultValue">The measured value, as the lab reports it.</param>
/// <param name="TestResult">The coded result — Positive/Reactive and the rest.</param>
/// <param name="Comment">The lab's free-text comment on the result.</param>
/// <param name="Units">The units the value is in.</param>
/// <param name="PrimaryLabResult">
/// Whether this is the primary result of its test; false where it is left out.
/// </param>
public record CreateLabTestResultRequest(
    int? OrganismId = null,
    string? ResultValue = null,
    string? TestResult = null,
    string? Comment = null,
    string? Units = null,
    bool PrimaryLabResult = false);
