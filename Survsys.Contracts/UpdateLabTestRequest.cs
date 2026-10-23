namespace Survsys.Contracts;

/// <summary>
/// The corrections to a stored test's own columns: what
/// <c>PUT /api/lab-tests/{id}</c> takes.
/// </summary>
/// <remarks>
/// The writable columns <see cref="CreateLabTestRequest"/> records, less the
/// results, which are corrected through <c>PUT /api/lab-test-results/{id}</c>:
/// a test edit is of the test row, not of the results hung off it. The
/// specimen a test belongs to is not here and does not change. Every field is
/// nullable, and the save is a whole replacement of the test's own columns, a
/// field sent null clearing it.
/// </remarks>
/// <param name="TestTypeId">What was tested for, as the legacy <c>test_type_id</c>.</param>
/// <param name="TestStatusId">
/// Where the test stands, as the legacy <c>test_status_id</c>.
/// </param>
/// <param name="LoincCode">The LOINC code of the test.</param>
/// <param name="ReferenceRange">The reference range the result is read against.</param>
/// <param name="LabTestDate">The date the test was performed.</param>
public record UpdateLabTestRequest(
    int? TestTypeId = null,
    int? TestStatusId = null,
    string? LoincCode = null,
    string? ReferenceRange = null,
    DateOnly? LabTestDate = null);
