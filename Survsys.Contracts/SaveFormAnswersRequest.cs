namespace Survsys.Contracts;

/// <summary>
/// A case's answers to an assigned form, as
/// <c>PUT /api/cases/forms/{assignmentId}/answers</c> takes them: one answer
/// per question the case is giving an answer to.
/// </summary>
/// <remarks>
/// The answers are given by question id, so the completion screen sends only
/// the questions it has an answer for, and each is upserted onto the one row
/// for that question of the assignment. A required question must come back with
/// an answer — a non-blank text for a text question, a code for a coded one —
/// and a coded answer's code must be one of the question's options, both of
/// which the endpoint refuses where they are not.
/// </remarks>
/// <param name="Answers">The answers, one per question being answered.</param>
public record SaveFormAnswersRequest(IReadOnlyList<FormAnswerInput> Answers);

/// <summary>One answer to one question of an assigned form.</summary>
/// <param name="QuestionId">The question being answered.</param>
/// <param name="TextAnswer">The text answer to a text question, or null.</param>
/// <param name="Code">The chosen code of a coded question, or null.</param>
public record FormAnswerInput(
    int QuestionId,
    string? TextAnswer = null,
    string? Code = null);
