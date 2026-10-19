namespace Survsys.Contracts;

/// <summary>
/// The clinical data to save on a case morbidity report: what
/// <c>PUT /api/person-conditions/{id}/clinical</c> takes.
/// </summary>
/// <remarks>
/// The same fields <see cref="ClinicalResponse"/> returns, less the report id,
/// which the route carries. Every field is nullable and the save is a whole
/// replacement of the report's clinical data: a field sent null clears it, as
/// the legacy Clinical tab does when its control is blanked.
///
/// The fields the legacy Clinical tab writes, which
/// <c>../nedss/docs/functional/epitrax-cmr-deep-editing.md</c> section 3 maps:
/// the disease onset date, the date diagnosed and whether treatment was given
/// are written to the report, and the died? answer and the date of death are
/// written to the report's <em>person</em>, because they are person-level and
/// shared across that person's reports.
/// </remarks>
/// <param name="DiseaseOnsetDate">The date the disease began in this person.</param>
/// <param name="DateDiagnosed">The date this case was diagnosed.</param>
/// <param name="TreatmentGiven">
/// Whether treatment was given, as the legacy Yes/No/Unknown code.
/// </param>
/// <param name="DeathStatus">
/// The report's person's died? answer, as the legacy Yes/No/Unknown code.
/// Written to the person.
/// </param>
/// <param name="DateOfDeath">
/// The date the report's person died. Written to the person.
/// </param>
/// <param name="Pregnant">Whether the person is pregnant: Yes, No or Unknown, or null where unanswered.</param>
/// <param name="PregnancyDueDate">The pregnancy's due date, or null.</param>
/// <param name="ExpectedDeliveryFacilityId">The facility the delivery is expected at, or null.</param>
public record UpdateClinicalRequest(
    DateOnly? DiseaseOnsetDate,
    DateOnly? DateDiagnosed,
    string? TreatmentGiven,
    string? DeathStatus,
    DateOnly? DateOfDeath,
    string? Pregnant = null,
    DateOnly? PregnancyDueDate = null,
    int? ExpectedDeliveryFacilityId = null);
