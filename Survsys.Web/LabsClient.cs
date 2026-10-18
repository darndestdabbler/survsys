using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of a report's laboratory
/// specimens: <c>GET</c> and <c>POST /api/person-conditions/{id}/labs</c>, and
/// <c>GET /api/labs/{id}</c> for one specimen whole.
/// </summary>
/// <remarks>
/// It is the only thing in this tier that speaks HTTP about laboratory
/// specimens, and the failure handling is <see cref="ApiAsk"/>'s, as every
/// client's is.
///
/// Three asks and three prefixes, because a user whose list of specimens could
/// not be loaded, one whose single specimen could not be loaded and one whose
/// new specimen was refused are three different sentences to read. The create
/// endpoint answers 201 with the specimen whole, so <see cref="Record"/> hands
/// back the same <see cref="LabDetailResponse"/> <see cref="Detail"/> does.
/// </remarks>
public sealed class LabsClient(HttpClient http)
{
    /// <summary>How a failure to list a report's specimens starts.</summary>
    public const string ListFailurePrefix = "The laboratory specimens could not be loaded";

    /// <summary>How a failure to read one specimen starts.</summary>
    public const string FindFailurePrefix = "The laboratory specimen could not be loaded";

    /// <summary>How a failure to record a new specimen starts.</summary>
    public const string RecordFailurePrefix = "The laboratory specimen could not be recorded";

    /// <summary>How a failure to save a correction to a laboratory row starts.</summary>
    public const string SaveFailurePrefix = "The laboratory record could not be saved";

    /// <summary>How a failure to remove a laboratory row starts.</summary>
    public const string RemoveFailurePrefix = "The laboratory record could not be removed";

    private static readonly ApiAsk Listing = new(ListFailurePrefix, "a page of laboratory specimens");

    private static readonly ApiAsk Finding = new(FindFailurePrefix, "a laboratory specimen");

    private static readonly ApiAsk Recording = new(RecordFailurePrefix, "the specimen recorded");

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the laboratory record as saved");

    private static readonly ApiAsk Removing = new(RemoveFailurePrefix, "the laboratory record removed");

    /// <summary>
    /// One page of a report's laboratory specimens, or why there is none.
    /// </summary>
    public Task<Asked<PagedResult<LabResponse>>> Page(
        int reportId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        Listing.Get<PagedResult<LabResponse>>(
            http,
            $"api/person-conditions/{reportId}/labs?page={page}&pageSize={pageSize}",
            cancellationToken);

    /// <summary>
    /// One laboratory specimen whole — its tests with their results and its
    /// susceptibility rows — or why there is none.
    /// </summary>
    public Task<Asked<LabDetailResponse>> Detail(
        int labId,
        CancellationToken cancellationToken = default) =>
        Finding.Get<LabDetailResponse>(http, $"api/labs/{labId}", cancellationToken);

    /// <summary>
    /// Records a laboratory specimen against a report and answers with it
    /// whole, or with why none was recorded.
    /// </summary>
    public Task<Asked<LabDetailResponse>> Record(
        int reportId,
        CreateLabRequest specimen,
        CancellationToken cancellationToken = default) =>
        Recording.Post<CreateLabRequest, LabDetailResponse>(
            http, $"api/person-conditions/{reportId}/labs", specimen, cancellationToken);

    /// <summary>
    /// Corrects a specimen's own columns and answers with it as stored, or with
    /// why nothing was saved.
    /// </summary>
    public Task<Asked<LabResponse>> UpdateSpecimen(
        int labId,
        UpdateLabRequest specimen,
        CancellationToken cancellationToken = default) =>
        Saving.Put<UpdateLabRequest, LabResponse>(
            http, $"api/labs/{labId}", specimen, cancellationToken);

    /// <summary>
    /// Corrects a test's own columns and answers with it whole, or with why
    /// nothing was saved.
    /// </summary>
    public Task<Asked<LabTestResponse>> UpdateTest(
        int testId,
        UpdateLabTestRequest test,
        CancellationToken cancellationToken = default) =>
        Saving.Put<UpdateLabTestRequest, LabTestResponse>(
            http, $"api/lab-tests/{testId}", test, cancellationToken);

    /// <summary>
    /// Corrects a test result and answers with it as stored, or with why
    /// nothing was saved — including the refusal of a second result marked the
    /// primary one.
    /// </summary>
    public Task<Asked<LabTestResultResponse>> UpdateResult(
        int resultId,
        CreateLabTestResultRequest result,
        CancellationToken cancellationToken = default) =>
        Saving.Put<CreateLabTestResultRequest, LabTestResultResponse>(
            http, $"api/lab-test-results/{resultId}", result, cancellationToken);

    /// <summary>
    /// Corrects an antimicrobial susceptibility row and answers with it as
    /// stored, or with why nothing was saved.
    /// </summary>
    public Task<Asked<LabResistTestResponse>> UpdateResist(
        int resistId,
        CreateLabResistTestRequest resist,
        CancellationToken cancellationToken = default) =>
        Saving.Put<CreateLabResistTestRequest, LabResistTestResponse>(
            http, $"api/lab-resist-tests/{resistId}", resist, cancellationToken);

    /// <summary>
    /// Removes a specimen, or answers with why it was not — a specimen that
    /// still has tests or susceptibility rows is refused, the 409 the API
    /// answers reaching the screen as the other failures do.
    /// </summary>
    public Task<string?> DeleteSpecimen(int labId, CancellationToken cancellationToken = default) =>
        Removing.Delete(http, $"api/labs/{labId}", cancellationToken);

    /// <summary>
    /// Removes a test, or answers with why it was not — a test that still has
    /// results is refused.
    /// </summary>
    public Task<string?> DeleteTest(int testId, CancellationToken cancellationToken = default) =>
        Removing.Delete(http, $"api/lab-tests/{testId}", cancellationToken);

    /// <summary>Removes a test result, or answers with why it was not.</summary>
    public Task<string?> DeleteResult(int resultId, CancellationToken cancellationToken = default) =>
        Removing.Delete(http, $"api/lab-test-results/{resultId}", cancellationToken);

    /// <summary>Removes an antimicrobial susceptibility row, or answers with why it was not.</summary>
    public Task<string?> DeleteResist(int resistId, CancellationToken cancellationToken = default) =>
        Removing.Delete(http, $"api/lab-resist-tests/{resistId}", cancellationToken);
}
