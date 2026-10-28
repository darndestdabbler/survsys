namespace Survsys.Data;

/// <summary>
/// One case status that makes a condition's cases notifiable to CDC: a case of
/// <see cref="ConditionId"/> whose <see cref="PersonCondition.CaseStatus"/> is
/// <see cref="CaseStatus"/> is queued for notification when it changes.
/// </summary>
/// <remarks>
/// The legacy NMI <c>nmi.condition_print_criteria</c>, as described in
/// <c>../nedss/docs/business-logic/NMI_EXPORT_PIPELINE.md</c>, keyed here on
/// the case status code <see cref="CaseStatusRules"/> holds rather than a
/// <c>case_status_id</c> into a lookup, because that is what a case carries.
/// A condition with no rows has no status that notifies it.
/// </remarks>
public class ConditionPrintCriterion
{
    /// <summary>The criterion's id. The key.</summary>
    public int Id { get; set; }

    /// <summary>The condition the criterion is for, as a bare id into <c>conditions</c>.</summary>
    public int ConditionId { get; set; }

    /// <summary>
    /// The case status code, from <see cref="CaseStatusRules"/>, that makes a
    /// case of the condition notifiable.
    /// </summary>
    public string CaseStatus { get; set; } = string.Empty;
}
