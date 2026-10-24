namespace Survsys.Contracts;

/// <summary>
/// The clinical data of a case morbidity report, as the API tier returns it.
/// </summary>
/// <remarks>
/// The fields the legacy Clinical tab reads and writes, which
/// <c>../nedss/docs/functional/epitrax-cmr-deep-editing.md</c> section 3 maps:
/// the disease onset date, the date diagnosed and whether treatment was given,
/// which are the report's own, together with the died? answer and the date of
/// death, which are the report's <em>person's</em> and are read from there. A
/// report's person is shared across that person's reports, so the two
/// person-level fields read the same on every report about them; the API tier
/// gathers both halves into this one shape so a Clinical tab reads one resource
/// rather than two.
/// </remarks>
/// <param name="ReportId">The report whose clinical data this is.</param>
/// <param name="DiseaseOnsetDate">
/// The date the disease began in this person, or null where it is not known.
/// </param>
/// <param name="DateDiagnosed">
/// The date this case was diagnosed, or null where it is not known.
/// </param>
/// <param name="TreatmentGiven">
/// Whether treatment was given, as the legacy Yes/No/Unknown code, or null
/// where it has not been answered.
/// </param>
/// <param name="DeathStatus">
/// The report's person's died? answer, as the legacy Yes/No/Unknown code, or
/// null where it has not been answered. Read from the person.
/// </param>
/// <param name="DateOfDeath">
/// The date the report's person died, or null where they have not or it is not
/// known. Read from the person.
/// </param>
/// <param name="Pregnant">Whether the person is pregnant: Yes, No or Unknown, or null where unanswered.</param>
/// <param name="PregnancyDueDate">The pregnancy's due date, or null.</param>
/// <param name="ExpectedDeliveryFacilityId">The facility the delivery is expected at, or null.</param>
public record ClinicalResponse(
    int ReportId,
    DateOnly? DiseaseOnsetDate,
    DateOnly? DateDiagnosed,
    string? TreatmentGiven,
    string? DeathStatus,
    DateOnly? DateOfDeath,
    string? Pregnant = null,
    DateOnly? PregnancyDueDate = null,
    int? ExpectedDeliveryFacilityId = null);
