namespace Survsys.Contracts;

/// <summary>
/// The investigation data of a case morbidity report, as the API tier returns
/// it.
/// </summary>
/// <remarks>
/// The fields the legacy Investigation tab reads and writes to the report
/// itself, which
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps under the Investigation tab's Field-to-DB Mapping: the four Contact
/// Oriented codes, then the Other section's imported-from key, transmission
/// mode, risk factors and the two catch-all fields. The tab's occupations,
/// exposures, associated forms and binational reporting criteria are each a
/// table of their own and are not part of this shape.
///
/// The coded fields carry the legacy code and the keys carry the legacy
/// identifier, with no reference list behind either: the session-012 fill
/// shows the codes as the lowercase <c>unknown</c> and the two-letter
/// transmission mode <c>AB</c>, and the imported-from key as the bare
/// <c>11</c>, and that is what this shape returns.
/// </remarks>
/// <param name="ReportId">The report whose investigation data this is.</param>
/// <param name="FoodHandler">
/// Whether the person handles food, as the legacy Yes/No/Unknown code, or
/// null where it has not been answered.
/// </param>
/// <param name="HealthcareWorker">
/// Whether the person works in health care, as the legacy Yes/No/Unknown
/// code, or null where it has not been answered.
/// </param>
/// <param name="GroupLiving">
/// Whether the person lives in a group setting, as the legacy Yes/No/Unknown
/// code, or null where it has not been answered.
/// </param>
/// <param name="DayCareAssociation">
/// Whether the person is associated with a day care, as the legacy
/// Yes/No/Unknown code, or null where it has not been answered.
/// </param>
/// <param name="ImportedFromId">
/// Where the condition was imported from, as the legacy imported-from
/// identifier, or null where it has not been chosen.
/// </param>
/// <param name="TransmissionMode">
/// How the condition was transmitted, as the legacy transmission-mode code,
/// or null where it has not been chosen.
/// </param>
/// <param name="RiskFactors">
/// The risk factors noted for this case, as free text, or null where none
/// have been entered.
/// </param>
/// <param name="RiskFactorsNotes">
/// Notes on the risk factors, as free text, or null where none have been
/// entered.
/// </param>
/// <param name="OtherData1">
/// The first catch-all field, as free text, or null where it is empty.
/// </param>
/// <param name="OtherData2">
/// The second catch-all field, as free text, or null where it is empty.
/// </param>
public record InvestigationResponse(
    int ReportId,
    string? FoodHandler,
    string? HealthcareWorker,
    string? GroupLiving,
    string? DayCareAssociation,
    int? ImportedFromId,
    string? TransmissionMode,
    string? RiskFactors,
    string? RiskFactorsNotes,
    string? OtherData1,
    string? OtherData2);
