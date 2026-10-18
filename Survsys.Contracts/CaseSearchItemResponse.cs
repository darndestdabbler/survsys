namespace Survsys.Contracts;

/// <summary>
/// A case morbidity report as one row of the case search.
/// </summary>
/// <remarks>
/// Unlike <see cref="WorkQueueItemResponse"/>, this row carries the patient's
/// name, the condition's name and the agency's name joined in, because the
/// screens that show it — the Events list and a person's cases — show them per
/// row and have no catalogue of people to resolve a name from. The identifiers
/// come too, so that a row can open its case, its patient, or be narrowed on
/// by its condition or agency without a second lookup.
///
/// It is the row the legacy Events list shows, as far as this schema reaches:
/// the patient and record number, the disease, the status, the investigating
/// agency, the workflow status and the event date, with the MMWR week the
/// plan asks for beside them.
/// </remarks>
/// <param name="Id">The report's identifier, as its own resource refers to it.</param>
/// <param name="RecordNumber">The number the report is known by.</param>
/// <param name="PersonId">The person the report is about.</param>
/// <param name="PatientName">
/// The patient as "Last, First", or the last name alone where no first name
/// is on file — the same reading the People results give a name.
/// </param>
/// <param name="ConditionId">The disease or condition the report is for.</param>
/// <param name="ConditionName">That condition's name.</param>
/// <param name="AgencyId">
/// The agency investigating the report, or null where none was chosen.
/// </param>
/// <param name="AgencyName">That agency's name, or null where there is no agency.</param>
/// <param name="WorkflowState">
/// Where the report sits in the lifecycle, as the legacy workflow code. Never
/// absent on the wire: the API tier reads a report with no stored code as the
/// assigned-to-LHD default and carries that code here.
/// </param>
/// <param name="CaseStatus">
/// The case classification code, or null for an unclassified case read as
/// Unknown.
/// </param>
/// <param name="MmwrWeek">The MMWR week the report falls in, or null where none is set.</param>
/// <param name="MmwrYear">The MMWR year that week belongs to, or null where none is set.</param>
/// <param name="EventOnsetDate">
/// The date the event began, or null where it is not set.
/// </param>
public record CaseSearchItemResponse(
    int Id,
    string RecordNumber,
    int PersonId,
    string PatientName,
    int ConditionId,
    string ConditionName,
    int? AgencyId,
    string? AgencyName,
    string WorkflowState,
    string? CaseStatus,
    int? MmwrWeek,
    int? MmwrYear,
    DateOnly? EventOnsetDate);
