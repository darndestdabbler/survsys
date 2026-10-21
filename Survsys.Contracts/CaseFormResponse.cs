namespace Survsys.Contracts;

/// <summary>
/// A form as the completion screen shows it for one case: the form's whole
/// tree of sections, questions and coded options, with the case's current
/// answer to each question beside it, so the screen shows the form filled as
/// far as it has been.
/// </summary>
/// <remarks>
/// This is <see cref="FormDetailResponse"/> seen through one assignment: the
/// same tree, in the same order, but each question carries the case's answer —
/// a <see cref="CaseFormQuestionResponse.TextAnswer"/> for a text question or a
/// <see cref="CaseFormQuestionResponse.Code"/> for a coded one — or null where
/// it is unanswered.
/// </remarks>
/// <param name="AssignmentId">The assignment this form is completed through.</param>
/// <param name="FormId">The form being completed.</param>
/// <param name="FormName">The form's name.</param>
/// <param name="Description">What the form is for, or null where unwritten.</param>
/// <param name="Sections">The form's sections, in order, with the case's answers.</param>
public record CaseFormResponse(
    int AssignmentId,
    int FormId,
    string FormName,
    string? Description,
    IReadOnlyList<CaseFormSectionResponse> Sections);

/// <summary>A section of a form being completed: its heading and its questions.</summary>
/// <param name="Id">The section's identifier.</param>
/// <param name="Title">The section's heading.</param>
/// <param name="SortOrder">Where the section sits among the form's sections.</param>
/// <param name="Questions">The section's questions, in order, with the case's answers.</param>
public record CaseFormSectionResponse(
    int Id,
    string Title,
    int SortOrder,
    IReadOnlyList<CaseFormQuestionResponse> Questions);

/// <summary>
/// A question of a form being completed: its prompt and choices, and the case's
/// current answer to it.
/// </summary>
/// <param name="Id">The question's identifier.</param>
/// <param name="Text">The question as asked.</param>
/// <param name="HelpText">A hint shown under the question, or null where none.</param>
/// <param name="AnswerType">How the question is answered: 0 free text, 1 a coded choice.</param>
/// <param name="IsRequired">Whether an answer is required.</param>
/// <param name="SortOrder">Where the question sits among its section's questions.</param>
/// <param name="Options">The choices of a coded question, in order; empty for a text question.</param>
/// <param name="TextAnswer">The case's text answer, or null where unanswered or coded.</param>
/// <param name="Code">The case's coded answer, or null where unanswered or free text.</param>
public record CaseFormQuestionResponse(
    int Id,
    string Text,
    string? HelpText,
    int AnswerType,
    bool IsRequired,
    int SortOrder,
    IReadOnlyList<FormQuestionOptionResponse> Options,
    string? TextAnswer,
    string? Code);
