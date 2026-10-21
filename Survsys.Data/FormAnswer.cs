namespace Survsys.Data;

/// <summary>
/// One answer to one question of an assigned form: the free text or the code a
/// case gave for a question.
/// </summary>
/// <remarks>
/// EpiTrax's own <c>answer</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, kept to the case answer: the legacy answer carries a
/// question, a <c>text_answer</c> and a <c>code</c>, and ties itself to a
/// person_condition, an exposure or an outbreak; this release ties the answer
/// to its <see cref="FormAssignmentId"/> — which already names the case — and
/// keeps the <see cref="TextAnswer"/> and the <see cref="Code"/>, leaving out
/// the exposure and outbreak links and the <c>export_conversion_value_id</c>
/// export mapping. A text question's answer is in <see cref="TextAnswer"/>; a
/// coded question's is a <see cref="Code"/> that is one of the question's
/// <see cref="FormQuestionOption"/> codes. A question is answered once per
/// assignment — the configuration keys the pair — so saving an answer again is
/// a save over the one row.
/// </remarks>
public class FormAnswer
{
    public int Id { get; set; }

    /// <summary>The assignment the answer belongs to.</summary>
    public int FormAssignmentId { get; set; }

    /// <summary>The question the answer is to.</summary>
    public int FormQuestionId { get; set; }

    /// <summary>
    /// The answer to a text question, as the legacy <c>text_answer</c> — free
    /// text, left unbounded as <see cref="Note.Text"/> is; null for a coded
    /// question or an unanswered one.
    /// </summary>
    public string? TextAnswer { get; set; }

    /// <summary>
    /// The answer to a coded question, as the legacy <c>code</c> — one of the
    /// question's <see cref="FormQuestionOption.Code"/> values; null for a text
    /// question or an unanswered one.
    /// </summary>
    public string? Code { get; set; }

    /// <summary>When the answer was saved, as the legacy <c>created_at</c>.</summary>
    /// <remarks>
    /// A <see cref="DateTimeOffset"/> as <see cref="FormAssignment.AssignedAt"/>
    /// is, carrying its offset. Required, and set where the answer is saved.
    /// </remarks>
    public DateTimeOffset CreatedAt { get; set; }
}
