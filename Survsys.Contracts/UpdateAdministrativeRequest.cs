namespace Survsys.Contracts;

/// <summary>
/// The administrative data to save on a case morbidity report: what
/// <c>PUT /api/person-conditions/{id}/administrative</c> takes.
/// </summary>
/// <remarks>
/// The same fields <see cref="AdministrativeResponse"/> returns, less the
/// report id, which the route carries. Every field is nullable and the save
/// is a whole replacement of the report's administrative data: a field sent
/// null clears it, as the legacy Administrative tab does when its control is
/// blanked or set back to its empty choice.
///
/// The writable fields the legacy Administrative tab writes to the report,
/// which
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps under the Administrative tab's Field-to-DB Mapping: the two
/// per-jurisdiction case-status keys, the outbreak-association code, the two
/// LHD investigation dates, the outcome key, the event name and the two
/// reported dates. The read-only Event Information the tab shows is not
/// writable and so not here, and the date first reported to public health is
/// the report's own and is not saved through this shape. The keys are sent
/// as the bare legacy identifiers and the code as the legacy writes it, the
/// capitalised <c>Unknown</c>; no list stands behind any of them here.
/// </remarks>
/// <param name="LhdCaseStatusId">
/// The local health department's case status, as the legacy case-status
/// identifier.
/// </param>
/// <param name="StateCaseStatusId">
/// The state's case status, as the legacy case-status identifier.
/// </param>
/// <param name="OutbreakAssociated">
/// Whether the case is associated with an outbreak, as the legacy
/// Yes/No/Unknown code.
/// </param>
/// <param name="LhdInvestigationStartDate">
/// The date the local health department started its investigation.
/// </param>
/// <param name="LhdInvestigationCompletedDate">
/// The date the local health department completed its investigation.
/// </param>
/// <param name="InvestigationOutcomeDispositionId">
/// The outcome of the investigation, as the legacy disposition identifier.
/// </param>
/// <param name="EventName">A name for the event this report is about, as free text.</param>
/// <param name="ResultsReportedToClinicianDate">
/// The date results were reported, which the tab labels "Results reported to
/// LHD".
/// </param>
/// <param name="CdcFirstVerballyNotifiedDate">
/// The date the CDC was first verbally notified.
/// </param>
public record UpdateAdministrativeRequest(
    int? LhdCaseStatusId,
    int? StateCaseStatusId,
    string? OutbreakAssociated,
    DateOnly? LhdInvestigationStartDate,
    DateOnly? LhdInvestigationCompletedDate,
    int? InvestigationOutcomeDispositionId,
    string? EventName,
    DateOnly? ResultsReportedToClinicianDate,
    DateOnly? CdcFirstVerballyNotifiedDate);
