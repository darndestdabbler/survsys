namespace Survsys.Contracts;

/// <summary>
/// One form as its builder shows it: the form, the condition it is for by name
/// as well as by id, and the whole tree of sections, their questions and each
/// coded question's options, every level in order.
/// </summary>
/// <remarks>
/// The detail adds to <see cref="FormResponse"/> what the one-form screen reads
/// and the list does not: the description and the nested tree. The sections are
/// in <see cref="FormSectionResponse.SortOrder"/> order, each section's
/// questions in <see cref="FormQuestionResponse.SortOrder"/> order, and a coded
/// question's options in <see cref="FormQuestionOptionResponse.SortOrder"/>
/// order.
/// </remarks>
/// <param name="Id">The form's identifier.</param>
/// <param name="Name">The form's name.</param>
/// <param name="Description">What the form is for, or null where unwritten.</param>
/// <param name="ConditionId">The condition the form is for.</param>
/// <param name="ConditionName">The condition's name, or null where it cannot be resolved.</param>
/// <param name="AutoAssign">Whether the form is assigned to a case automatically.</param>
/// <param name="CreatedAt">When the form was created.</param>
/// <param name="Sections">The form's sections, in order.</param>
public record FormDetailResponse(
    int Id,
    string Name,
    string? Description,
    int ConditionId,
    string? ConditionName,
    bool AutoAssign,
    DateTimeOffset CreatedAt,
    IReadOnlyList<FormSectionResponse> Sections);

/// <summary>
/// A section of a form as the builder shows it: its heading and the questions
/// it holds, in order.
/// </summary>
/// <param name="Id">The section's identifier.</param>
/// <param name="Title">The section's heading.</param>
/// <param name="SortOrder">Where the section sits among the form's sections.</param>
/// <param name="Questions">The section's questions, in order.</param>
public record FormSectionResponse(
    int Id,
    string Title,
    int SortOrder,
    IReadOnlyList<FormQuestionResponse> Questions);

/// <summary>
/// A question of a section as the builder shows it: its prompt, how it is
/// answered, whether it is required, and — for a coded question — its choices.
/// </summary>
/// <param name="Id">The question's identifier.</param>
/// <param name="Text">The question as asked.</param>
/// <param name="HelpText">A hint shown under the question, or null where none.</param>
/// <param name="AnswerType">How the question is answered: 0 free text, 1 a coded choice.</param>
/// <param name="IsRequired">Whether an answer is required.</param>
/// <param name="SortOrder">Where the question sits among its section's questions.</param>
/// <param name="Options">The choices of a coded question, in order; empty for a text question.</param>
public record FormQuestionResponse(
    int Id,
    string Text,
    string? HelpText,
    int AnswerType,
    bool IsRequired,
    int SortOrder,
    IReadOnlyList<FormQuestionOptionResponse> Options);

/// <summary>
/// One allowed choice of a coded question: the code stored when it is picked
/// and the label shown for it.
/// </summary>
/// <param name="Id">The choice's identifier.</param>
/// <param name="Code">The value stored when the choice is picked.</param>
/// <param name="Label">The text shown for the choice.</param>
/// <param name="SortOrder">Where the choice sits among the question's choices.</param>
public record FormQuestionOptionResponse(
    int Id,
    string Code,
    string Label,
    int SortOrder);
