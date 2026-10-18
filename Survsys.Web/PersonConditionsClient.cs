using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's writer and reader of <c>/api/person-conditions</c>:
/// the case morbidity report the creation flow opens, and the one the report's
/// own form shows.
/// </summary>
/// <remarks>
/// It is the only thing in this tier that speaks HTTP about reports, and the
/// failure handling is <see cref="ApiAsk"/>'s, as every client's is.
///
/// Session 14 wrote it to open a report and not to read one back, because the
/// screen that shows a report was this session's and a read written then
/// would have been a method with no caller. The report shell is that caller,
/// and <see cref="Find"/> is the read: the two asks carry two prefixes, since
/// a user whose report was not created and one whose report could not be
/// shown are being told two different things.
/// </remarks>
public sealed class PersonConditionsClient(HttpClient http)
{
    /// <summary>
    /// How a failure to open a report starts. Public because the screens and
    /// the unit suite read it rather than repeating the wording.
    /// </summary>
    public const string FailurePrefix = "The case morbidity report could not be created";

    /// <summary>How a failure to read one report starts.</summary>
    public const string FindFailurePrefix = "The case morbidity report could not be loaded";

    /// <summary>How a failure to set a report's case status starts.</summary>
    public const string StatusFailurePrefix = "The case morbidity report status could not be saved";

    /// <summary>How a failure to make a workflow move on a report starts.</summary>
    public const string WorkflowFailurePrefix = "The case morbidity report workflow could not be advanced";

    /// <summary>How a failure to read a report's workflow starts.</summary>
    public const string WorkflowLoadFailurePrefix = "The case morbidity report workflow could not be loaded";

    private static readonly ApiAsk Opening = new(FailurePrefix, "the report created");

    private static readonly ApiAsk Finding = new(FindFailurePrefix, "the report");

    private static readonly ApiAsk Setting = new(StatusFailurePrefix, "the report as saved");

    private static readonly ApiAsk Advancing = new(WorkflowFailurePrefix, "the report as advanced");

    private static readonly ApiAsk LoadingWorkflow = new(WorkflowLoadFailurePrefix, "the report's workflow");

    /// <summary>
    /// Opens a case morbidity report and answers with it — its record number
    /// among it, which is what the screen shows next — or with why none was
    /// opened.
    /// </summary>
    public Task<Asked<PersonConditionResponse>> Open(
        CreatePersonConditionRequest report,
        CancellationToken cancellationToken = default) =>
        Opening.Post<CreatePersonConditionRequest, PersonConditionResponse>(
            http, "api/person-conditions", report, cancellationToken);

    /// <summary>
    /// One case morbidity report, or why there is none — an identifier
    /// nobody has among the reasons, which the API tier answers with a 404.
    /// </summary>
    /// <remarks>
    /// What the report shell needs first: the report says who it is about
    /// and what it is for, and the shell reads those from their own
    /// resources once it has this.
    /// </remarks>
    public Task<Asked<PersonConditionResponse>> Find(
        int id,
        CancellationToken cancellationToken = default) =>
        Finding.Get<PersonConditionResponse>(http, $"api/person-conditions/{id}", cancellationToken);

    /// <summary>
    /// Sets a report's case status and answers with the report as the API tier
    /// now holds it, or with why nothing was saved.
    /// </summary>
    /// <remarks>
    /// A third prefix, because a user whose status was refused is being told a
    /// different thing than one whose report could not be created or shown. The
    /// answer is the whole <see cref="PersonConditionResponse"/>, so the screen
    /// shows the status as stored rather than as typed, and the vocabulary rule
    /// stays the API tier's — a code it refuses comes back as this failure.
    /// </remarks>
    public Task<Asked<PersonConditionResponse>> SetStatus(
        int id,
        UpdateCaseStatusRequest status,
        CancellationToken cancellationToken = default) =>
        Setting.Put<UpdateCaseStatusRequest, PersonConditionResponse>(
            http, $"api/person-conditions/{id}/status", status, cancellationToken);

    /// <summary>
    /// Makes a workflow move on a report — routing it to an agency among them
    /// — and answers with the report as the API tier now holds it, or with why
    /// it was not moved.
    /// </summary>
    /// <remarks>
    /// A move the transition table or the caller's privileges refuse comes back
    /// as this failure, since the rule is the API tier's: a screen offers the
    /// moves the API lists and reads a refusal as a message rather than
    /// re-checking it. The answer is the whole report, so the screen shows the
    /// state and the agency as stored.
    /// </remarks>
    public Task<Asked<PersonConditionResponse>> Transition(
        int id,
        WorkflowTransitionRequest transition,
        CancellationToken cancellationToken = default) =>
        Advancing.Put<WorkflowTransitionRequest, PersonConditionResponse>(
            http, $"api/person-conditions/{id}/workflow", transition, cancellationToken);

    /// <summary>
    /// A report's workflow as the signed-in user sees it — its state, the moves
    /// they may make and its history — or why it could not be read.
    /// </summary>
    public Task<Asked<WorkflowResponse>> Workflow(
        int id,
        CancellationToken cancellationToken = default) =>
        LoadingWorkflow.Get<WorkflowResponse>(http, $"api/person-conditions/{id}/workflow", cancellationToken);
}
