namespace Survsys.Contracts;

/// <summary>
/// A supplemental form, as the forms list shows it: the form's own columns,
/// with the condition it is for named for the row and its auto-assign flag.
/// </summary>
/// <remarks>
/// The list row carries the condition's name, which the list query joins on,
/// so the forms screen shows what a form is for without a second call; the
/// whole section/question/option tree is <see cref="FormDetailResponse"/>,
/// which the one-form screen reads.
/// </remarks>
/// <param name="Id">The form's identifier.</param>
/// <param name="Name">The form's name, as a list and a dropdown show it.</param>
/// <param name="ConditionId">The condition the form is for.</param>
/// <param name="ConditionName">The condition's name, or null where it cannot be resolved.</param>
/// <param name="AutoAssign">Whether the form is assigned to a case automatically.</param>
/// <param name="CreatedAt">When the form was created.</param>
public record FormResponse(
    int Id,
    string Name,
    int ConditionId,
    string? ConditionName,
    bool AutoAssign,
    DateTimeOffset CreatedAt);
