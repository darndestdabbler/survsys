namespace Survsys.Contracts;

/// <summary>
/// A case morbidity report, as the API tier returns it.
/// </summary>
/// <remarks>
/// What this phase stores about a report: who it is about, what it is for, the
/// number it is known by, the agency investigating it, the two dates and when
/// it was created, and — as of session 20 — the case status and the MMWR week
/// and year. The remaining fields of the legacy table belong to a later
/// session, and so does every name behind an identifier here — the person's
/// name and the condition's are read from their own resources, because a
/// report that carried them would be two resources in one and would go stale
/// when either changed.
///
/// <see cref="RecordNumber"/> is the field a user is shown next: the legacy
/// flow ends by naming it, and it is how a report is referred to afterwards.
/// </remarks>
/// <param name="Id">The report's identifier, as every other resource refers to it.</param>
/// <param name="PersonId">The person the report is about.</param>
/// <param name="ConditionId">The disease or condition the report is for.</param>
/// <param name="RecordNumber">
/// The number the report is known by, in the legacy <c>YYYYNNNNN</c> format —
/// the year it was created in and the sequence within that year.
/// </param>
/// <param name="AgencyId">
/// The agency investigating the report, or null where none was chosen.
/// </param>
/// <param name="FirstReportedPhDate">
/// The date the condition was first reported to public health, or null where
/// it is not known.
/// </param>
/// <param name="EventOnsetDate">
/// The date the event began. Set to the creation date when the report is
/// created, which is what the legacy flow does; the Clinical tab is where it
/// is corrected.
/// </param>
/// <param name="CreatedAt">When the report was created.</param>
/// <param name="CaseStatus">
/// The case classification, as the legacy status code — one of Confirmed,
/// Probable, Suspected or Not a Case — or null until a status is set, which is
/// read as Unknown. Set directly rather than derived; the API tier validates
/// the code against the case status vocabulary.
/// </param>
/// <param name="MmwrWeek">
/// The MMWR week the report's event is counted in, computed from
/// <paramref name="EventOnsetDate"/> when the report is opened, or null where
/// there is no onset date.
/// </param>
/// <param name="MmwrYear">
/// The MMWR year the report's event is counted in, which can differ from the
/// onset date's calendar year at either end of it; null where there is no
/// onset date, and read together with <paramref name="MmwrWeek"/>.
/// </param>
/// <param name="WorkflowState">
/// Where the report sits in the investigation lifecycle, as the legacy workflow
/// code — one of assigned to LHD, accepted, under investigation or closed — or
/// null for a report read as the assigned-to-LHD default a case starts at. The
/// vocabulary and the moves between states are the API tier's, which validates
/// a transition against the workflow rules.
/// </param>
/// <param name="InvestigatorId">
/// The user investigating the report, named by the assign-investigator
/// transition, or null where none is assigned.
/// </param>
/// <param name="EventType">
/// What kind of event this is — <c>morbidity</c>, <c>contact</c> or
/// <c>assessment</c>.
/// </param>
/// <param name="BriefNote">The header's brief note, or null where none is written.</param>
/// <param name="LhdCaseStatusId">The local health department's case status, as stored, or null.</param>
/// <param name="StateCaseStatusId">The state's case status, as stored, or null.</param>
public record PersonConditionResponse(
    int Id,
    int PersonId,
    int ConditionId,
    string RecordNumber,
    int? AgencyId,
    DateOnly? FirstReportedPhDate,
    DateOnly? EventOnsetDate,
    DateTimeOffset CreatedAt,
    string? CaseStatus,
    int? MmwrWeek,
    int? MmwrYear,
    string? WorkflowState,
    int? InvestigatorId = null,
    string EventType = "morbidity",
    string? BriefNote = null,
    int? LhdCaseStatusId = null,
    int? StateCaseStatusId = null);
