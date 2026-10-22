namespace Survsys.Data;

/// <summary>
/// A supplemental form: a named, condition-specific questionnaire a public
/// health agency builds to gather more than the core case record holds, filled
/// in against a case.
/// </summary>
/// <remarks>
/// The columns are EpiTrax's own <c>form</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, kept to the lean set this release builds a form with:
/// its name and description, the condition it is for, whether it is assigned to
/// a case automatically, and when it was created. The legacy's
/// <c>is_template</c>, <c>template_id</c>, <c>version</c>, <c>status</c>,
/// <c>rolled_back_from_id</c>, <c>event_type</c> and <c>short_name</c> — the
/// template, versioning and rollback machinery — are left out, and the legacy
/// <c>condition_form</c> / <c>condition_form_auto</c> join tables are collapsed
/// to a single <see cref="ConditionId"/> with an <see cref="AutoAssign"/> flag:
/// a form names its one condition directly.
///
/// <see cref="Name"/> is required, as <see cref="Outbreak.Name"/> is: a form
/// with no name cannot be chosen from a list. <see cref="ConditionId"/> is a
/// real foreign key that does not cascade — a condition with forms against it
/// cannot be deleted out from under them — and it is required, because a form
/// is condition-specific. The nested sections are <see cref="FormSection"/>,
/// each section's questions <see cref="FormQuestion"/> and a coded question's
/// choices <see cref="FormQuestionOption"/>; a form is given to a case through
/// <see cref="FormAssignment"/> and answered through <see cref="FormAnswer"/>.
/// </remarks>
public class Form
{
    public int Id { get; set; }

    /// <summary>The form's name, as a dropdown and a list show it.</summary>
    public required string Name { get; set; }

    /// <summary>
    /// What the form is for, as the legacy <c>description</c> — free text,
    /// left unbounded as <see cref="Note.Text"/> is; null until given.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The condition the form is for, as the legacy <c>condition_form</c>
    /// join collapsed to a column. A real foreign key that does not cascade.
    /// </summary>
    public int ConditionId { get; set; }

    /// <summary>
    /// Whether the form is assigned to a case automatically when a case is
    /// opened for its condition, as the legacy <c>condition_form_auto</c> join
    /// collapsed to a flag; false until set.
    /// </summary>
    public bool AutoAssign { get; set; }

    /// <summary>When the form was created, as the legacy <c>created_at</c>.</summary>
    /// <remarks>
    /// A <see cref="DateTimeOffset"/> as <see cref="Outbreak.CreatedAt"/> is:
    /// the moment the form was created, carrying its offset. Required, and set
    /// where the form is created.
    /// </remarks>
    public DateTimeOffset CreatedAt { get; set; }
}
