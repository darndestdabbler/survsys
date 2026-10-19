namespace Survsys.Contracts;

/// <summary>
/// A form to create or to save over an existing one, with its whole tree of
/// sections, questions and coded options in one call: what
/// <c>POST /api/forms</c> and <c>PUT /api/forms/{id}</c> take.
/// </summary>
/// <remarks>
/// The name and the condition are required; a form is condition-specific, so
/// the condition must be one this tier knows, which the endpoint refuses where
/// it is not. The order of each level is the order of its list — the first
/// section is first, the first question in a section is first — so the builder
/// sends the tree as it should read and the server numbers it. A text question
/// carries no options; a coded question carries at least one.
/// </remarks>
/// <param name="Name">The form's name. Required.</param>
/// <param name="ConditionId">The condition the form is for. Required.</param>
/// <param name="AutoAssign">Whether to assign the form to a case automatically.</param>
/// <param name="Description">What the form is for, or null for none.</param>
/// <param name="Sections">The form's sections, in the order they should read.</param>
public record SaveFormRequest(
    string Name,
    int ConditionId,
    bool AutoAssign = false,
    string? Description = null,
    IReadOnlyList<SaveFormSectionRequest>? Sections = null);

/// <summary>A section to write, with the questions it holds.</summary>
/// <param name="Title">The section's heading. Required.</param>
/// <param name="Questions">The section's questions, in the order they should read.</param>
public record SaveFormSectionRequest(
    string Title,
    IReadOnlyList<SaveFormQuestionRequest>? Questions = null);

/// <summary>A question to write, with its choices where it is coded.</summary>
/// <param name="Text">The question as asked. Required.</param>
/// <param name="AnswerType">How it is answered: 0 free text, 1 a coded choice.</param>
/// <param name="IsRequired">Whether an answer is required.</param>
/// <param name="HelpText">A hint shown under the question, or null for none.</param>
/// <param name="Options">The choices of a coded question; none for a text question.</param>
public record SaveFormQuestionRequest(
    string Text,
    int AnswerType = 0,
    bool IsRequired = false,
    string? HelpText = null,
    IReadOnlyList<SaveFormQuestionOptionRequest>? Options = null);

/// <summary>A coded question's choice to write.</summary>
/// <param name="Code">The value stored when the choice is picked. Required.</param>
/// <param name="Label">The text shown for the choice. Required.</param>
public record SaveFormQuestionOptionRequest(
    string Code,
    string Label);
