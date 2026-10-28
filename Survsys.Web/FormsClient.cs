using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of <c>/api/forms</c>: the
/// condition-specific supplemental forms an operator builds, their listing and
/// their whole-tree save.
/// </summary>
/// <remarks>
/// It is the only thing in this tier that speaks HTTP about forms, and the
/// failure handling is <see cref="ApiAsk"/>'s, as every client's is.
///
/// The list asks for one wide page, as <see cref="OutbreaksClient"/> asks for
/// the outbreaks: the screen shows the forms in one list rather than a pager,
/// and the page still comes back in <see cref="PagedResult{T}"/>, the one
/// collection shape this solution puts on the wire.
/// </remarks>
public sealed class FormsClient(HttpClient http)
{
    /// <summary>
    /// How every failure to load the list starts. Public because the screens
    /// and the unit suite read it rather than repeating the wording.
    /// </summary>
    public const string FailurePrefix = "The forms could not be loaded";

    /// <summary>How a failure to load one form starts.</summary>
    public const string FindFailurePrefix = "The form could not be loaded";

    /// <summary>How a failure to create or save a form starts.</summary>
    public const string SaveFailurePrefix = "The form could not be saved";

    /// <summary>How a failure to load a case's assigned forms starts.</summary>
    public const string CaseFormsFailurePrefix = "The case's forms could not be loaded";

    /// <summary>How a failure to load one assigned form starts.</summary>
    public const string CaseFormFailurePrefix = "The assigned form could not be loaded";

    /// <summary>How a failure to save a case's answers starts.</summary>
    public const string AnswersSaveFailurePrefix = "The answers could not be saved";

    /// <summary>How a failure to assign a form to a case starts.</summary>
    public const string AssignFailurePrefix = "The form could not be assigned";

    /// <summary>The largest page the list asks for, wide enough to be the only one.</summary>
    private const int PageSize = 200;

    private static readonly ApiAsk Ask = new(FailurePrefix, "a list of forms");

    private static readonly ApiAsk Finding = new(FindFailurePrefix, "the form's record");

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the form as saved");

    private static readonly ApiAsk AskingCaseForms = new(CaseFormsFailurePrefix, "the case's forms");

    private static readonly ApiAsk AskingCaseForm = new(CaseFormFailurePrefix, "the assigned form");

    private static readonly ApiAsk SavingAnswers = new(AnswersSaveFailurePrefix, "the answers as saved");

    private static readonly ApiAsk Assigning = new(AssignFailurePrefix, "the form's assignment");

    /// <summary>Every form, newest first, or why there is no list.</summary>
    public Task<Asked<PagedResult<FormResponse>>> All(
        CancellationToken cancellationToken = default) =>
        Ask.Get<PagedResult<FormResponse>>(
            http,
            $"api/forms?pageSize={PageSize}",
            cancellationToken);

    /// <summary>
    /// One form with its sections, questions and coded options in order, or why
    /// there is none.
    /// </summary>
    public Task<Asked<FormDetailResponse>> Find(
        int id,
        CancellationToken cancellationToken = default) =>
        Finding.Get<FormDetailResponse>(http, $"api/forms/{id}", cancellationToken);

    /// <summary>Builds a form, answering with it as created or why it was not.</summary>
    public Task<Asked<FormDetailResponse>> Create(
        SaveFormRequest request,
        CancellationToken cancellationToken = default) =>
        Saving.Post<SaveFormRequest, FormDetailResponse>(
            http,
            "api/forms",
            request,
            cancellationToken);

    /// <summary>Saves over a form, answering with it as saved or why it was not.</summary>
    public Task<Asked<FormDetailResponse>> Update(
        int id,
        SaveFormRequest request,
        CancellationToken cancellationToken = default) =>
        Saving.Put<SaveFormRequest, FormDetailResponse>(
            http,
            $"api/forms/{id}",
            request,
            cancellationToken);

    /// <summary>The forms assigned to a case, to complete, or why there is no list.</summary>
    public Task<Asked<List<FormAssignmentResponse>>> CaseForms(
        int personConditionId,
        CancellationToken cancellationToken = default) =>
        AskingCaseForms.Get<List<FormAssignmentResponse>>(
            http,
            $"api/cases/{personConditionId}/forms",
            cancellationToken);

    /// <summary>
    /// Assigns a form to a case, answering with the assignment or why there is
    /// none. The route names both, so the post carries no body.
    /// </summary>
    public async Task<Asked<FormAssignmentResponse>> Assign(
        int personConditionId,
        int formId,
        CancellationToken cancellationToken = default)
    {
        using var nothing = new ByteArrayContent([]);

        return await Assigning.PostContent<FormAssignmentResponse>(
            http,
            $"api/cases/{personConditionId}/forms/{formId}",
            nothing,
            cancellationToken);
    }

    /// <summary>
    /// One assigned form, its tree and the case's answers so far, or why there
    /// is none.
    /// </summary>
    public Task<Asked<CaseFormResponse>> CaseForm(
        int assignmentId,
        CancellationToken cancellationToken = default) =>
        AskingCaseForm.Get<CaseFormResponse>(
            http,
            $"api/cases/forms/{assignmentId}",
            cancellationToken);

    /// <summary>
    /// Saves a case's answers to an assigned form, answering with the form as
    /// saved or why it was not.
    /// </summary>
    public Task<Asked<CaseFormResponse>> SaveAnswers(
        int assignmentId,
        SaveFormAnswersRequest request,
        CancellationToken cancellationToken = default) =>
        SavingAnswers.Put<SaveFormAnswersRequest, CaseFormResponse>(
            http,
            $"api/cases/forms/{assignmentId}/answers",
            request,
            cancellationToken);
}
