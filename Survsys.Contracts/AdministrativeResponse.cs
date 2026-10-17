namespace Survsys.Contracts;

/// <summary>
/// The administrative data of a case morbidity report, as the API tier
/// returns it.
/// </summary>
/// <remarks>
/// The writable fields the legacy Administrative tab reads and writes to the
/// report itself, which
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps under the Administrative tab's Field-to-DB Mapping: from the Case /
/// Outbreak section the two per-jurisdiction case-status keys and the
/// outbreak-association code; from Auditing / Investigation the two LHD
/// investigation dates, the outcome key and the event name; and from Reported
/// Dates the two dates the tab lets an investigator enter.
///
/// The read-only Event Information the tab shows first — the record number,
/// the MMWR week and year and the date the record was created — is already
/// carried by <see cref="PersonConditionResponse"/> and is not duplicated
/// here, and the date first reported to public health is read from there
/// too. The tab's outbreak name, queue, tags and reporter are each a table of
/// their own and are not part of this shape.
///
/// The keys carry the legacy identifier and the code carries the legacy code,
/// with no reference list behind either: the session-012 fill shows the three
/// keys as the bare <c>11</c> and the outbreak association as
/// <c>Unknown</c>, capitalised where the Investigation codes are lowercase,
/// and that is what this shape returns.
/// </remarks>
/// <param name="ReportId">The report whose administrative data this is.</param>
/// <param name="LhdCaseStatusId">
/// The local health department's case status, as the legacy case-status
/// identifier, or null where it has not been chosen.
/// </param>
/// <param name="StateCaseStatusId">
/// The state's case status, as the legacy case-status identifier, or null
/// where it has not been chosen.
/// </param>
/// <param name="OutbreakAssociated">
/// Whether the case is associated with an outbreak, as the legacy
/// Yes/No/Unknown code, or null where it has not been answered.
/// </param>
/// <param name="LhdInvestigationStartDate">
/// The date the local health department started its investigation, or null
/// where it has not been entered.
/// </param>
/// <param name="LhdInvestigationCompletedDate">
/// The date the local health department completed its investigation, or
/// null where it has not been entered.
/// </param>
/// <param name="InvestigationOutcomeDispositionId">
/// The outcome of the investigation, as the legacy disposition identifier,
/// or null where it has not been chosen.
/// </param>
/// <param name="EventName">
/// A name for the event this report is about, as free text, or null where
/// none has been entered.
/// </param>
/// <param name="ResultsReportedToClinicianDate">
/// The date results were reported, which the tab labels "Results reported to
/// LHD", or null where it has not been entered.
/// </param>
/// <param name="CdcFirstVerballyNotifiedDate">
/// The date the CDC was first verbally notified, or null where it has not
/// been entered.
/// </param>
/// <param name="OutbreakId">
/// The outbreak this case belongs to, as the Case / Outbreak section's
/// "Outbreak name" names it, or null where the case names no outbreak.
/// </param>
/// <param name="OutbreakName">
/// The name of the outbreak this case belongs to, for the tab to show without
/// a second read, or null where the case names no outbreak.
/// </param>
public record AdministrativeResponse(
    int ReportId,
    int? LhdCaseStatusId,
    int? StateCaseStatusId,
    string? OutbreakAssociated,
    DateOnly? LhdInvestigationStartDate,
    DateOnly? LhdInvestigationCompletedDate,
    int? InvestigationOutcomeDispositionId,
    string? EventName,
    DateOnly? ResultsReportedToClinicianDate,
    DateOnly? CdcFirstVerballyNotifiedDate,
    int? OutbreakId = null,
    string? OutbreakName = null);
