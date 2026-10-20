namespace Survsys.Contracts;

/// <summary>
/// A reviewer's confirmed decision to promote a staged message to a case: what
/// <c>POST /api/staged-messages/{id}/case</c> takes.
/// </summary>
/// <remarks>
/// The four things a case morbidity report is opened from — the person the
/// report is about, the disease it is for, the agency investigating it and the
/// date the disease was first reported to public health — the same four
/// <c>CreatePersonConditionRequest</c> carries, because promotion opens a
/// report and a report is opened from these. They are a distinct shape rather
/// than that one reused, because a reviewer confirms them against a message
/// rather than keying them into a blank creation screen, and the promotion
/// endpoint is where that confirmation is recorded.
///
/// <strong>Nothing here is read from the message.</strong> The parsed person
/// and specimen a message carries are prompts for the reviewer, not the
/// decision: a case is created only from what the reviewer confirms, never
/// derived from the message without them, which is what makes the creation
/// semi-automated rather than automatic. The record number is assigned by the
/// tier that stores the report, as it is for a report opened directly.
/// </remarks>
/// <param name="PersonId">The person the promoted report is about.</param>
/// <param name="ConditionId">
/// The disease or condition the promoted report is for, which must be one that
/// may be chosen for a new report.
/// </param>
/// <param name="AgencyId">
/// The agency investigating the report, or null where none has been chosen.
/// Nullable because a report can exist without one.
/// </param>
/// <param name="FirstReportedPhDate">
/// The date the condition was first reported to public health, where it is
/// known.
/// </param>
/// <param name="PersonConditionId">
/// The person's existing case the message updates, in place of opening a new
/// one; null opens a new case. The case is updated as automatic assignment
/// updates one, by the condition's whitelist rule and a comparison of labs,
/// so the condition, agency and date are not read for it.
/// </param>
public record PromoteMessageRequest(
    int PersonId,
    int ConditionId,
    int? AgencyId = null,
    DateOnly? FirstReportedPhDate = null,
    int? PersonConditionId = null);
