namespace Survsys.Messaging;

/// <summary>
/// What <see cref="CdaParser"/> makes of one eCR document: an eICR or the
/// reportability response (RR) made for one, as the fields the later eCR
/// sessions read.
/// </summary>
/// <param name="Kind">
/// <see cref="CdaParser.EicrKind"/> or <see cref="CdaParser.RrKind"/>.
/// </param>
/// <param name="IgVersion">The implementation guide version the document declares, such as <c>R1.1</c> or <c>R3.1.1</c>.</param>
/// <param name="DocumentId">The document's id (ClinicalDocument/id), as <see cref="CdaParser.FormatId"/> writes it.</param>
/// <param name="SetId">The id shared by every version of the document (ClinicalDocument/setId), or null where it has none.</param>
/// <param name="VersionNumber">The document's version within its set (ClinicalDocument/versionNumber), or null where it has none.</param>
/// <param name="Created">When the document was created (ClinicalDocument/effectiveTime), or null where it is absent or unreadable.</param>
/// <param name="Custodian">The custodian organisation's id, else its name: the sender of the document.</param>
/// <param name="Patient">The patient (recordTarget), with one order per result organizer.</param>
/// <param name="Encounter">The encounter the document reports (componentOf/encompassingEncounter), or null.</param>
/// <param name="Problems">Every problem observation, in document order.</param>
/// <param name="Medications">Every medication activity, in document order.</param>
/// <param name="Pregnancy">The first pregnancy observation, or null.</param>
/// <param name="Travel">Every travel history entry, in document order.</param>
/// <param name="Occupations">Every occupation observation, in document order.</param>
/// <param name="EicrDocumentId">For an RR, the id of the eICR it responds to; null for an eICR.</param>
/// <param name="Conditions">For an RR, each condition with its determination for one responsible agency; empty for an eICR.</param>
public record CdaDocument(
    string Kind,
    string IgVersion,
    string DocumentId,
    string? SetId,
    int? VersionNumber,
    DateTimeOffset? Created,
    string? Custodian,
    ParsedPatient Patient,
    CdaEncounter? Encounter,
    IReadOnlyList<CdaProblem> Problems,
    IReadOnlyList<CdaMedication> Medications,
    CdaPregnancy? Pregnancy,
    IReadOnlyList<CdaTravel> Travel,
    IReadOnlyList<CdaOccupation> Occupations,
    string? EicrDocumentId,
    IReadOnlyList<RrCondition> Conditions);

/// <summary>A coded value: its code, code system OID and display name.</summary>
public record CdaCode(string? Code, string? CodeSystem, string? DisplayName);

/// <summary>The encounter an eICR reports.</summary>
/// <param name="Id">The encounter's id.</param>
/// <param name="Type">The encounter's type code.</param>
/// <param name="Start">When it began.</param>
/// <param name="End">When it ended, where it has.</param>
/// <param name="FacilityId">The facility's id.</param>
/// <param name="FacilityName">The facility's name.</param>
public record CdaEncounter(
    string? Id, CdaCode? Type, DateOnly? Start, DateOnly? End, string? FacilityId, string? FacilityName);

/// <summary>A problem observation: a diagnosis, symptom or finding.</summary>
/// <param name="Value">The problem.</param>
/// <param name="Onset">When it began.</param>
/// <param name="Resolved">When it resolved, where it has.</param>
/// <param name="IsTrigger">Whether the EHR flagged it as the trigger code that caused the report.</param>
public record CdaProblem(CdaCode? Value, DateOnly? Onset, DateOnly? Resolved, bool IsTrigger);

/// <summary>A medication activity.</summary>
/// <param name="Drug">The medication.</param>
/// <param name="Start">When it was given or began.</param>
/// <param name="Dose">The dose, as its value and unit.</param>
/// <param name="Route">The route's display name.</param>
public record CdaMedication(CdaCode? Drug, DateOnly? Start, string? Dose, string? Route);

/// <summary>The patient's pregnancy status.</summary>
/// <param name="Status">The status asserted, such as 77386006 Pregnant.</param>
/// <param name="Negated">Whether the observation asserts the status did not hold.</param>
/// <param name="From">The start of the period it covers.</param>
/// <param name="To">The end of the period it covers.</param>
public record CdaPregnancy(CdaCode? Status, bool Negated, DateOnly? From, DateOnly? To);

/// <summary>A travel history entry.</summary>
/// <param name="Location">Where the patient went: a coded place, else the address given.</param>
/// <param name="Text">The entry's free text.</param>
/// <param name="From">When the travel began.</param>
/// <param name="To">When it ended.</param>
public record CdaTravel(string? Location, string? Text, DateOnly? From, DateOnly? To);

/// <summary>An occupation the patient holds or held.</summary>
/// <param name="Occupation">The coded occupation, where it is coded.</param>
/// <param name="Text">The occupation as text, where it is given as text.</param>
/// <param name="From">When it began.</param>
/// <param name="To">When it ended, where it has.</param>
public record CdaOccupation(CdaCode? Occupation, string? Text, DateOnly? From, DateOnly? To);

/// <summary>
/// One condition an RR reports, for one responsible agency: the condition
/// with the determination that agency's rules made.
/// </summary>
/// <param name="Condition">The condition, as its SNOMED code.</param>
/// <param name="Determination">The determination, RRVS1 reportable to RRVS4 no rule met.</param>
/// <param name="Location">Whose address the determination was made for (RRVS5 home, RRVS6 provider, RRVS7 both).</param>
/// <param name="ResponsibleAgencyId">The responsible agency's id.</param>
/// <param name="ResponsibleAgencyName">The responsible agency's name.</param>
/// <param name="ReportingTimeframe">The time to report within, as its value and unit.</param>
public record RrCondition(
    CdaCode? Condition,
    CdaCode? Determination,
    CdaCode? Location,
    string? ResponsibleAgencyId,
    string? ResponsibleAgencyName,
    string? ReportingTimeframe);
