using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of <c>/api/outbreaks</c>: the
/// outbreaks a case can be linked to, their administration and their
/// multi-agency access.
/// </summary>
/// <remarks>
/// It is the only thing in this tier that speaks HTTP about outbreaks, and the
/// failure handling is <see cref="ApiAsk"/>'s, as every client's is.
///
/// The list asks for one wide page, as <see cref="AgenciesClient"/> asks for
/// the agencies: the screen shows the outbreaks in one list rather than a
/// pager, and the page still comes back in <see cref="PagedResult{T}"/>, the
/// one collection shape this solution puts on the wire.
/// </remarks>
public sealed class OutbreaksClient(HttpClient http)
{
    /// <summary>
    /// How every failure to load the list starts. Public because the screens
    /// and the unit suite read it rather than repeating the wording.
    /// </summary>
    public const string FailurePrefix = "The outbreaks could not be loaded";

    /// <summary>How a failure to load one outbreak starts.</summary>
    public const string FindFailurePrefix = "The outbreak could not be loaded";

    /// <summary>How a failure to create or save an outbreak starts.</summary>
    public const string SaveFailurePrefix = "The outbreak could not be saved";

    /// <summary>How a failure to load an outbreak's access agencies starts.</summary>
    public const string AccessFailurePrefix = "The outbreak access could not be loaded";

    /// <summary>How a failure to save an outbreak's access agencies starts.</summary>
    public const string AccessSaveFailurePrefix = "The outbreak access could not be saved";

    /// <summary>How a failure to load the report sections starts.</summary>
    public const string SectionsFailurePrefix = "The report sections could not be loaded";

    /// <summary>How a failure to load an outbreak's report starts.</summary>
    public const string ReportFailurePrefix = "The outbreak report could not be loaded";

    /// <summary>How a failure to save a section of an outbreak's report starts.</summary>
    public const string ReportSaveFailurePrefix = "The outbreak report could not be saved";

    /// <summary>How a failure to load an outbreak's activity log starts.</summary>
    public const string ActivityFailurePrefix = "The activity log could not be loaded";

    /// <summary>How a failure to append an activity entry starts.</summary>
    public const string ActivityAddFailurePrefix = "The activity entry could not be added";

    /// <summary>How a failure to load an outbreak's linked cases starts.</summary>
    public const string CasesFailurePrefix = "The linked cases could not be loaded";

    /// <summary>The largest page the list asks for, wide enough to be the only one.</summary>
    private const int PageSize = 200;

    private static readonly ApiAsk Ask = new(FailurePrefix, "a list of outbreaks");

    private static readonly ApiAsk Finding = new(FindFailurePrefix, "the outbreak's record");

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the outbreak as saved");

    private static readonly ApiAsk AskingAccess = new(AccessFailurePrefix, "the outbreak's access agencies");

    private static readonly ApiAsk SavingAccess = new(AccessSaveFailurePrefix, "the access as saved");

    private static readonly ApiAsk AskingSections = new(SectionsFailurePrefix, "the report sections");

    private static readonly ApiAsk AskingReport = new(ReportFailurePrefix, "the outbreak's report");

    private static readonly ApiAsk SavingReport = new(ReportSaveFailurePrefix, "the report as saved");

    private static readonly ApiAsk AskingActivity = new(ActivityFailurePrefix, "the activity log");

    private static readonly ApiAsk AddingActivity = new(ActivityAddFailurePrefix, "the entry as added");

    private static readonly ApiAsk AskingCases = new(CasesFailurePrefix, "the linked cases");

    /// <summary>Every outbreak, newest first, or why there is no list.</summary>
    public Task<Asked<PagedResult<OutbreakResponse>>> All(
        CancellationToken cancellationToken = default) =>
        Ask.Get<PagedResult<OutbreakResponse>>(
            http,
            $"api/outbreaks?pageSize={PageSize}",
            cancellationToken);

    /// <summary>
    /// One outbreak with its condition and owning agency named and its access
    /// agencies, or why there is none.
    /// </summary>
    public Task<Asked<OutbreakDetailResponse>> Find(
        int id,
        CancellationToken cancellationToken = default) =>
        Finding.Get<OutbreakDetailResponse>(http, $"api/outbreaks/{id}", cancellationToken);

    /// <summary>Opens an outbreak, answering with it as created or why it was not.</summary>
    public Task<Asked<OutbreakDetailResponse>> Create(
        SaveOutbreakRequest request,
        CancellationToken cancellationToken = default) =>
        Saving.Post<SaveOutbreakRequest, OutbreakDetailResponse>(
            http,
            "api/outbreaks",
            request,
            cancellationToken);

    /// <summary>Saves over an outbreak, answering with it as saved or why it was not.</summary>
    public Task<Asked<OutbreakDetailResponse>> Update(
        int id,
        SaveOutbreakRequest request,
        CancellationToken cancellationToken = default) =>
        Saving.Put<SaveOutbreakRequest, OutbreakDetailResponse>(
            http,
            $"api/outbreaks/{id}",
            request,
            cancellationToken);

    /// <summary>The agencies granted sight of the outbreak, or why there is no list.</summary>
    public Task<Asked<List<OutbreakAccessResponse>>> Access(
        int id,
        CancellationToken cancellationToken = default) =>
        AskingAccess.Get<List<OutbreakAccessResponse>>(
            http,
            $"api/outbreaks/{id}/access",
            cancellationToken);

    /// <summary>Replaces the outbreak's access agencies, answering with the new list.</summary>
    public Task<Asked<List<OutbreakAccessResponse>>> SetAccess(
        int id,
        SetOutbreakAccessRequest request,
        CancellationToken cancellationToken = default) =>
        SavingAccess.Put<SetOutbreakAccessRequest, List<OutbreakAccessResponse>>(
            http,
            $"api/outbreaks/{id}/access",
            request,
            cancellationToken);

    /// <summary>The report sections an outbreak's report is written under, in order.</summary>
    public Task<Asked<List<ReportSectionResponse>>> ReportSections(
        CancellationToken cancellationToken = default) =>
        AskingSections.Get<List<ReportSectionResponse>>(http, "api/report-sections", cancellationToken);

    /// <summary>The outbreak's report as every section, written or not.</summary>
    public Task<Asked<List<OutbreakReportSectionResponse>>> Report(
        int id,
        CancellationToken cancellationToken = default) =>
        AskingReport.Get<List<OutbreakReportSectionResponse>>(
            http,
            $"api/outbreaks/{id}/report",
            cancellationToken);

    /// <summary>Writes one section of the outbreak's report, answering with the whole report.</summary>
    public Task<Asked<List<OutbreakReportSectionResponse>>> SaveReportSection(
        int id,
        int sectionId,
        SaveOutbreakReportSectionRequest request,
        CancellationToken cancellationToken = default) =>
        SavingReport.Put<SaveOutbreakReportSectionRequest, List<OutbreakReportSectionResponse>>(
            http,
            $"api/outbreaks/{id}/report/{sectionId}",
            request,
            cancellationToken);

    /// <summary>The outbreak's activity log, newest first, or why there is none.</summary>
    public Task<Asked<List<OutbreakUpdateResponse>>> Activity(
        int id,
        CancellationToken cancellationToken = default) =>
        AskingActivity.Get<List<OutbreakUpdateResponse>>(
            http,
            $"api/outbreaks/{id}/activity",
            cancellationToken);

    /// <summary>Appends one entry to the outbreak's activity log, answering with it.</summary>
    public Task<Asked<OutbreakUpdateResponse>> AddActivity(
        int id,
        AddOutbreakUpdateRequest request,
        CancellationToken cancellationToken = default) =>
        AddingActivity.Post<AddOutbreakUpdateRequest, OutbreakUpdateResponse>(
            http,
            $"api/outbreaks/{id}/activity",
            request,
            cancellationToken);

    /// <summary>The cases linked to the outbreak, newest first, or why there is no list.</summary>
    public Task<Asked<List<OutbreakCaseResponse>>> Cases(
        int id,
        CancellationToken cancellationToken = default) =>
        AskingCases.Get<List<OutbreakCaseResponse>>(http, $"api/outbreaks/{id}/cases", cancellationToken);
}
