namespace Survsys.Data;

/// <summary>
/// A question in a section of a form: its prompt, how it is answered — free
/// text or a coded choice — whether an answer is required, and where it sits
/// in its section.
/// </summary>
/// <remarks>
/// EpiTrax's own <c>form_element</c>/<c>question</c> pair, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>: the legacy carries a question as a <c>form_element</c>
/// of the question kind with a <c>question</c> row beside it; this release keeps
/// the question as one row under its <see cref="FormSectionId"/>. The legacy's
/// export-mapping and core-data columns (<c>export_column_id</c>,
/// <c>export_conversion_value_id</c>, <c>core_data</c>, <c>core_path</c>) are
/// left out: an answer is stored as entered, not projected to an export column
/// or bound to a core element. A coded question's choices are
/// <see cref="FormQuestionOption"/>.
/// </remarks>
public class FormQuestion
{
    public int Id { get; set; }

    /// <summary>The section the question belongs to.</summary>
    public int FormSectionId { get; set; }

    /// <summary>
    /// The question as asked, as the legacy <c>question.question_text</c>.
    /// </summary>
    public required string Text { get; set; }

    /// <summary>
    /// A hint shown under the question, as the legacy <c>question.help_text</c>;
    /// null until given.
    /// </summary>
    public string? HelpText { get; set; }

    /// <summary>
    /// How the question is answered, as the legacy <c>question.data_type</c>
    /// reduced to the two this release offers: 0 free text, 1 a coded choice
    /// from <see cref="FormQuestionOption"/>. 0 until set.
    /// </summary>
    public int AnswerType { get; set; }

    /// <summary>
    /// Whether an answer is required, as the legacy <c>question.is_required</c>;
    /// false until set.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Where the question sits among its section's questions; a plain ordinal
    /// as <see cref="FormSection.SortOrder"/> is.
    /// </summary>
    public int SortOrder { get; set; }
}
