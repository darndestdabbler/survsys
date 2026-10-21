namespace Survsys.Data;

/// <summary>
/// A section of a form: a titled grouping of questions, shown in order on the
/// form.
/// </summary>
/// <remarks>
/// EpiTrax's own <c>form_element</c> table of the section kind, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>: the legacy nests sections and questions in one
/// self-referencing <c>form_element</c> tree with <c>lft</c>/<c>rgt</c> bounds;
/// this release keeps the two levels a supplemental form needs — a form has
/// sections, a section has questions — as plain tables, so a section is a row
/// under its <see cref="FormId"/> with a <see cref="Title"/> and a
/// <see cref="SortOrder"/>, and the nested-set machinery is left out. The
/// questions are <see cref="FormQuestion"/>.
/// </remarks>
public class FormSection
{
    public int Id { get; set; }

    /// <summary>The form the section belongs to.</summary>
    public int FormId { get; set; }

    /// <summary>
    /// The section's heading, as the legacy <c>form_element.name</c>.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Where the section sits among the form's sections, as the legacy
    /// <c>form_element.lft</c> orders the tree; a plain ordinal here.
    /// </summary>
    public int SortOrder { get; set; }
}
