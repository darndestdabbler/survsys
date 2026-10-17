namespace Survsys.Contracts;

/// <summary>
/// The investigation data to save on a case morbidity report: what
/// <c>PUT /api/person-conditions/{id}/investigation</c> takes.
/// </summary>
/// <remarks>
/// The same fields <see cref="InvestigationResponse"/> returns, less the
/// report id, which the route carries. Every field is nullable and the save is
/// a whole replacement of the report's investigation data: a field sent null
/// clears it, as the legacy Investigation tab does when its control is
/// blanked or set back to "Please select".
///
/// The fields the legacy Investigation tab writes to the report, which
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps under the Investigation tab's Field-to-DB Mapping: the four Contact
/// Oriented codes, the imported-from key, the transmission-mode code and the
/// four free-text fields. The codes are sent as the legacy writes them — the
/// lowercase <c>unknown</c>, the two-letter <c>AB</c> — and the key as the
/// bare identifier; no list stands behind any of them here.
/// </remarks>
/// <param name="FoodHandler">
/// Whether the person handles food, as the legacy Yes/No/Unknown code.
/// </param>
/// <param name="HealthcareWorker">
/// Whether the person works in health care, as the legacy Yes/No/Unknown
/// code.
/// </param>
/// <param name="GroupLiving">
/// Whether the person lives in a group setting, as the legacy Yes/No/Unknown
/// code.
/// </param>
/// <param name="DayCareAssociation">
/// Whether the person is associated with a day care, as the legacy
/// Yes/No/Unknown code.
/// </param>
/// <param name="ImportedFromId">
/// Where the condition was imported from, as the legacy imported-from
/// identifier.
/// </param>
/// <param name="TransmissionMode">
/// How the condition was transmitted, as the legacy transmission-mode code.
/// </param>
/// <param name="RiskFactors">The risk factors noted for this case, as free text.</param>
/// <param name="RiskFactorsNotes">Notes on the risk factors, as free text.</param>
/// <param name="OtherData1">The first catch-all field, as free text.</param>
/// <param name="OtherData2">The second catch-all field, as free text.</param>
public record UpdateInvestigationRequest(
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
