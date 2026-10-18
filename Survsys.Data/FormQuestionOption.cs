namespace Survsys.Data;

/// <summary>
/// One allowed choice of a coded question: the code stored when the choice is
/// picked and the label shown for it.
/// </summary>
/// <remarks>
/// EpiTrax's own code set behind a coded <c>answer</c>, as the
/// <c>answer.code</c> column in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c> implies: a coded question is answered with one of a
/// fixed set of codes, so this release holds that set as the question's own
/// rows. A text question carries none. The <see cref="Code"/> is what a
/// <see cref="FormAnswer.Code"/> is validated against and stored as.
/// </remarks>
public class FormQuestionOption
{
    public int Id { get; set; }

    /// <summary>The question the choice belongs to.</summary>
    public int FormQuestionId { get; set; }

    /// <summary>The value stored when the choice is picked.</summary>
    public required string Code { get; set; }

    /// <summary>The text shown for the choice.</summary>
    public required string Label { get; set; }

    /// <summary>
    /// Where the choice sits among the question's choices; a plain ordinal as
    /// <see cref="FormQuestion.SortOrder"/> is.
    /// </summary>
    public int SortOrder { get; set; }
}
