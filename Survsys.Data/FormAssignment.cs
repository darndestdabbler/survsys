namespace Survsys.Data;

/// <summary>
/// A form given to a case: the link that says this case has this supplemental
/// form to fill in, whether chosen by hand or assigned automatically when the
/// case was opened for the form's condition.
/// </summary>
/// <remarks>
/// EpiTrax's own <c>form_reference</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, kept to the case link: the legacy references a form
/// from a person_condition, an exposure or an outbreak, and this release keeps
/// only <see cref="PersonConditionId"/> — a form is assigned to and answered
/// against a case this session, not an exposure or an outbreak. A form is
/// assigned once to a case — the configuration keys the pair — so assigning it
/// again is the same row. The answers are <see cref="FormAnswer"/>.
/// </remarks>
public class FormAssignment
{
    public int Id { get; set; }

    /// <summary>The form that was assigned.</summary>
    public int FormId { get; set; }

    /// <summary>The case the form was assigned to.</summary>
    public int PersonConditionId { get; set; }

    /// <summary>When the form was assigned to the case.</summary>
    /// <remarks>
    /// A <see cref="DateTimeOffset"/> as <see cref="Form.CreatedAt"/> is,
    /// carrying its offset. Required, and set where the form is assigned.
    /// </remarks>
    public DateTimeOffset AssignedAt { get; set; }
}
