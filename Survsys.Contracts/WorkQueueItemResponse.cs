namespace Survsys.Contracts;

/// <summary>
/// A case morbidity report as a row of an agency's work queue.
/// </summary>
/// <remarks>
/// The report's own columns, with no person or condition name joined: the
/// queue carries what a row shows of the report itself — its number, who and
/// what it is about by identifier, the agency, the workflow state, the case
/// status and the onset date — and the screen resolves a condition's name from
/// the conditions it already holds rather than the API carrying it per row.
///
/// <see cref="Shared"/> is the one field not stored on the report: it is true
/// when the row is in this queue because the report is shared read-only to the
/// agency asked about, and false when the agency is the one investigating it.
/// The screen reads it to know which rows it may act on.
/// </remarks>
/// <param name="Id">The report's identifier, as its own resource refers to it.</param>
/// <param name="RecordNumber">The number the report is known by.</param>
/// <param name="PersonId">The person the report is about.</param>
/// <param name="ConditionId">The disease or condition the report is for.</param>
/// <param name="AgencyId">
/// The agency investigating the report, or null where none was chosen.
/// </param>
/// <param name="WorkflowState">
/// Where the report sits in the lifecycle, as the legacy workflow code. Never
/// absent on the wire: the API tier reads a report with no stored code as the
/// assigned-to-LHD default and carries that code here.
/// </param>
/// <param name="CaseStatus">
/// The case classification code, or null for an unclassified case read as
/// Unknown.
/// </param>
/// <param name="EventOnsetDate">
/// The date the event began, or null where it is not set.
/// </param>
/// <param name="Shared">
/// True when the row is in the queue because the report is shared read-only to
/// the agency asked about, rather than investigated by it.
/// </param>
public record WorkQueueItemResponse(
    int Id,
    string RecordNumber,
    int PersonId,
    int ConditionId,
    int? AgencyId,
    string WorkflowState,
    string? CaseStatus,
    DateOnly? EventOnsetDate,
    bool Shared);
