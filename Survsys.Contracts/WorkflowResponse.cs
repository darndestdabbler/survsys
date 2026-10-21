namespace Survsys.Contracts;

/// <summary>
/// Where a case morbidity report sits in the workflow and what the caller may
/// do about it: what <c>GET /api/person-conditions/{id}/workflow</c> answers.
/// </summary>
/// <remarks>
/// <paramref name="Options"/> is already narrowed to the caller: the
/// transitions the report's state allows that the caller also holds the
/// privilege for. A screen offers exactly these and repeats no rule of its own,
/// which is what keeps the workflow table in one place.
/// </remarks>
/// <param name="ReportId">The report.</param>
/// <param name="WorkflowState">The report's state, as its code; a report with none stored reads as assigned to LHD.</param>
/// <param name="InvestigatorId">The user investigating the report, or null where none is assigned.</param>
/// <param name="Options">The transitions the caller may make from here, as their codes, in the order the workflow lists them.</param>
/// <param name="History">Every move the report has made, newest first.</param>
public record WorkflowResponse(
    int ReportId,
    string WorkflowState,
    int? InvestigatorId,
    IReadOnlyList<string> Options,
    IReadOnlyList<WorkflowHistoryEntryResponse> History);
