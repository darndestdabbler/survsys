using System.Text;
using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader of <c>GET /api/conditions</c>.
/// </summary>
/// <remarks>
/// It is the only thing in this tier that speaks HTTP about conditions, and it
/// swallows no failure silently: each one comes back as a sentence beginning
/// <see cref="FailurePrefix"/>, which is what the screen shows.
///
/// The failure handling itself is <see cref="ApiAsk"/>'s. It was written here
/// in session 6, when this was the only client in the tier; it moved when
/// there were four, for the reason the page-bounds rule moved out of
/// <c>ConditionEndpoints</c> into <c>Paging</c> — a second copy of a contract
/// is a second contract, and they agree until one of them is changed. Every
/// message a user can see is the same sentence it was before the move.
/// </remarks>
public sealed class ConditionsClient(HttpClient http)
{
    /// <summary>
    /// How every failure message starts. Public because the screen's own
    /// tests and the unit suite both read it rather than repeating the
    /// wording.
    /// </summary>
    public const string FailurePrefix = "The conditions could not be loaded";

    /// <summary>How a failure to load one condition starts.</summary>
    public const string FindFailurePrefix = "The condition could not be loaded";

    /// <summary>How a failure to create or save a condition starts.</summary>
    public const string SaveFailurePrefix = "The condition could not be saved";

    private static readonly ApiAsk Ask = new(FailurePrefix, "a page of conditions");

    private static readonly ApiAsk Finding = new(FindFailurePrefix, "the condition's record");

    /// <summary>How a failure to import a CSV file starts.</summary>
    public const string ImportFailurePrefix = "The CSV file could not be imported";

    /// <summary>How a failure to export the conditions starts.</summary>
    public const string ExportFailurePrefix = "The conditions could not be exported";

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the condition as saved");

    private static readonly ApiAsk Importing = new(ImportFailurePrefix, "what the import did");

    private static readonly ApiAsk Exporting = new(ExportFailurePrefix, "the conditions as CSV");

    /// <summary>How a failure to load a condition's print criteria starts.</summary>
    public const string PrintCriteriaFailurePrefix = "The condition's print criteria could not be loaded";

    /// <summary>How a failure to save a condition's print criteria starts.</summary>
    public const string PrintCriteriaSaveFailurePrefix = "The condition's print criteria could not be saved";

    private static readonly ApiAsk ReadingCriteria = new(PrintCriteriaFailurePrefix, "the condition's print criteria");

    private static readonly ApiAsk SavingCriteria = new(PrintCriteriaSaveFailurePrefix, "the print criteria as saved");

    /// <summary>
    /// The case statuses that make the condition's cases notifiable to CDC, or
    /// why there are none.
    /// </summary>
    public Task<Asked<ConditionPrintCriteriaResponse>> PrintCriteria(
        int id,
        CancellationToken cancellationToken = default) =>
        ReadingCriteria.Get<ConditionPrintCriteriaResponse>(
            http, $"api/conditions/{id}/print-criteria", cancellationToken);

    /// <summary>
    /// Replaces the case statuses that make the condition's cases notifiable,
    /// answering with them as saved or with why they were not.
    /// </summary>
    public Task<Asked<ConditionPrintCriteriaResponse>> SetPrintCriteria(
        int id,
        IReadOnlyList<string> caseStatuses,
        CancellationToken cancellationToken = default) =>
        SavingCriteria.Put<ConditionPrintCriteriaRequest, ConditionPrintCriteriaResponse>(
            http,
            $"api/conditions/{id}/print-criteria",
            new ConditionPrintCriteriaRequest(caseStatuses),
            cancellationToken);

    /// <summary>Every condition as CSV, or why there is none.</summary>
    public Task<Asked<string>> Export(CancellationToken cancellationToken = default) =>
        Exporting.GetText(http, "api/conditions/csv", cancellationToken);

    /// <summary>
    /// What importing a CSV file of conditions would do, saved only where
    /// <paramref name="confirm"/> is true, or why it did nothing.
    /// </summary>
    public async Task<Asked<ReferenceImportResponse>> Import(
        string csv, bool confirm, CancellationToken cancellationToken = default)
    {
        using var content = new StringContent(csv, Encoding.UTF8, "text/csv");
        return await Importing.PostContent<ReferenceImportResponse>(
            http, $"api/conditions/csv{(confirm ? "?confirm=true" : "")}", content, cancellationToken);
    }

    /// <summary>One condition with its lead-in prose, or why there is none.</summary>
    public Task<Asked<ConditionDetailResponse>> Find(
        int id,
        CancellationToken cancellationToken = default) =>
        Finding.Get<ConditionDetailResponse>(http, $"api/conditions/{id}", cancellationToken);

    /// <summary>Creates a condition, answering with it as created or why it was not.</summary>
    public Task<Asked<ConditionDetailResponse>> Create(
        SaveConditionRequest request,
        CancellationToken cancellationToken = default) =>
        Saving.Post<SaveConditionRequest, ConditionDetailResponse>(http, "api/conditions", request, cancellationToken);

    /// <summary>Saves over a condition, answering with it as saved or why it was not.</summary>
    public Task<Asked<ConditionDetailResponse>> Update(
        int id,
        SaveConditionRequest request,
        CancellationToken cancellationToken = default) =>
        Saving.Put<SaveConditionRequest, ConditionDetailResponse>(http, $"api/conditions/{id}", request, cancellationToken);

    /// <summary>
    /// One page of conditions, or why there is none.
    /// </summary>
    public Task<Asked<PagedResult<ConditionResponse>>> Page(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        Ask.Get<PagedResult<ConditionResponse>>(
            http, $"api/conditions?page={page}&pageSize={pageSize}", cancellationToken);
}
