namespace Survsys.Data;

/// <summary>
/// One lab result as the classification rules read it: the organism it
/// identifies, its test type, its coded result, and whether the test type is
/// one the case's condition is offered (a <see cref="CodeValueCondition"/>
/// row — the legacy <c>lab_test_type_condition</c>, the "approved test type").
/// </summary>
public sealed record ClassificationLab(string? Organism, string? TestType, string? TestResult, bool ApprovedTestType);

/// <summary>
/// What the classification rules read of a case: its condition's name, its
/// event type, workflow state, status and agency, the state of the patient's
/// current address and the agency its zip resolves to, its labs, and the three
/// facts a single rule needs — a facility encounter (influenza), and symptoms
/// and an epidemiological link (COVID-19).
/// </summary>
public sealed record ClassificationInput(
    string ConditionName,
    string EventType,
    string? WorkflowState,
    string? CaseStatus,
    int? AgencyId,
    string? PatientState,
    int? ZipAgencyId,
    IReadOnlyList<ClassificationLab> Labs,
    bool FacilityEncounter = false,
    bool Symptomatic = false,
    bool EpiLink = false);

/// <summary>
/// What the classification rules decide for a case: the status and agency it
/// should hold, whether it is a contact to close, and one line per rule that
/// had something to say, which is what the user is shown.
/// </summary>
public sealed record ClassificationOutcome(
    string? CaseStatus, int? AgencyId, bool CloseContact, IReadOnlyList<string> Findings);

/// <summary>
/// The validation and classification rules a case runs through on every save,
/// as <c>../nedss/docs/business-logic/CLASSIFICATION_RULES.md</c> describes
/// them: contact closure (section 2.1 step 2), out-of-state status and the
/// jurisdiction from the patient's address (sections 2.1 and 5), and one rule
/// per condition (section 3).
/// </summary>
/// <remarks>
/// Pure, in the shape <see cref="CaseStatusRules"/> has: the caller reads the
/// case and hands its facts over. Connecticut stands where the legacy has
/// Utah. A rule sets a status only where the case has none, or is Out of State
/// and no longer out of state, so a status a person chose is never
/// overwritten; a case leaving Out of State without a rule to classify it is
/// left Unknown.
/// </remarks>
public static class ClassificationRules
{
    /// <summary>The state whose residents' cases are classified here.</summary>
    public const string HomeState = "CT";

    /// <summary>The line the chlamydia rule gives a case with no valid lab, as the legacy note words it.</summary>
    public const string NoValidLab = "Auto Validation Error: No valid lab exists.";

    private enum Disease { Other, Chlamydia, Gonorrhea, HepatitisC, Influenza, Syphilis, Measles, Covid }

    /// <summary>Runs every rule that applies to the case and gathers what they decide.</summary>
    public static ClassificationOutcome Classify(ClassificationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var disease = DiseaseOf(input.ConditionName);
        var findings = new List<string>();
        var status = string.IsNullOrWhiteSpace(input.CaseStatus) ? null : input.CaseStatus.Trim();
        var agency = input.AgencyId;

        var closeContact = input.EventType == EventTypes.Contact
            && disease is Disease.Chlamydia or Disease.Gonorrhea or Disease.Syphilis
            && input.WorkflowState == WorkflowRules.AcceptedCode;
        if (closeContact)
        {
            findings.Add("Contact: a chlamydia, gonorrhea or syphilis contact accepted by the LHD is closed.");
        }

        var state = AddressRules.StateCode(input.PatientState);
        if (state is not null && state != HomeState)
        {
            if (status is null)
            {
                status = CaseStatusRules.OutOfStateCode;
                findings.Add($"Out of state: the patient's address is in {state}, so the case is Out of State.");
            }

            return new(status, agency, closeContact, findings);
        }

        if (status == CaseStatusRules.OutOfStateCode)
        {
            status = null;
            findings.Add("Out of state: the patient's address is no longer out of state, so the case is reclassified.");
        }

        if (agency is null && input.ZipAgencyId is { } zipAgency)
        {
            agency = zipAgency;
            findings.Add("Jurisdiction: the case is assigned to the agency the patient's zip code is in.");
        }

        var (ruled, finding) = disease switch
        {
            Disease.Chlamydia => Chlamydia(input.Labs),
            Disease.Gonorrhea => Gonorrhea(input.Labs),
            Disease.HepatitisC => HepatitisC(input.Labs),
            Disease.Influenza => Influenza(input.ConditionName, input.FacilityEncounter),
            Disease.Syphilis => (null, "Syphilis: the status is left to the investigation."),
            Disease.Measles => (null, "Measles: the status is left to the investigation."),
            Disease.Covid => Covid(input.Labs, input.Symptomatic, input.EpiLink),
            _ => (null, null),
        };

        if (finding is not null)
        {
            findings.Add(finding);
        }

        status ??= ruled;

        return new(status, agency, closeContact, findings);
    }

    /// <summary>A positive <i>Chlamydia trachomatis</i> result on an approved test type is Confirmed (section 3.1).</summary>
    private static (string?, string?) Chlamydia(IReadOnlyList<ClassificationLab> labs) =>
        labs.Any(lab => Names(lab.Organism, "Chlamydia trachomatis") && IsPositive(lab) && lab.ApprovedTestType)
            ? (CaseStatusRules.ConfirmedCode, "Chlamydia: a positive Chlamydia trachomatis lab confirms the case.")
            : (null, NoValidLab);

    /// <summary>
    /// A positive <i>Neisseria gonorrhoeae</i> result on an approved test type
    /// other than a Gram stain is Confirmed, and on a Gram stain Probable
    /// (section 3.2).
    /// </summary>
    private static (string?, string?) Gonorrhea(IReadOnlyList<ClassificationLab> labs)
    {
        var positive = labs.Where(lab => Names(lab.Organism, "Neisseria gonorrhoeae") && IsPositive(lab)).ToList();

        if (positive.Any(lab => lab.ApprovedTestType && !IsGramStain(lab)))
        {
            return (CaseStatusRules.ConfirmedCode, "Gonorrhea: a positive Neisseria gonorrhoeae lab confirms the case.");
        }

        return positive.Any(IsGramStain)
            ? (CaseStatusRules.ProbableCode, "Gonorrhea: a positive Gram stain makes the case probable.")
            : (null, "Gonorrhea: no positive Neisseria gonorrhoeae lab classifies the case.");
    }

    /// <summary>
    /// A positive hepatitis C virus result by RNA, PCR, genotype or antigen —
    /// the tests that show the virus itself — is Confirmed (section 3.3).
    /// </summary>
    private static (string?, string?) HepatitisC(IReadOnlyList<ClassificationLab> labs) =>
        labs.Any(lab => Names(lab.Organism, "Hepatitis C") && IsPositive(lab)
                && Has(lab.TestType, "RNA", "PCR", "amplification", "Genotype", "Antigen"))
            ? (CaseStatusRules.ConfirmedCode, "Hepatitis C: a positive hepatitis C virus lab confirms the case.")
            : (null, "Hepatitis C: no positive hepatitis C virus lab classifies the case.");

    /// <summary>
    /// An influenza-associated hospitalization needs its hospitalization on
    /// record, which here is an encounter at a facility (section 3.4). It
    /// assigns no status.
    /// </summary>
    private static (string?, string?) Influenza(string conditionName, bool facilityEncounter) =>
        Has(conditionName, "hospitalization") && !facilityEncounter
            ? (null, "Auto Validation Error: Influenza: no hospitalization is recorded.")
            : (null, null);

    /// <summary>
    /// The COVID-19 matrix (section 3.7) for a Connecticut patient: a positive
    /// PCR/amplification or antigen result is Confirmed, whatever else the case
    /// holds; a positive IgG antibody with an epidemiological link is
    /// Probable; and a positive test with neither symptoms nor a link is Not a
    /// Case.
    /// </summary>
    private static (string?, string?) Covid(IReadOnlyList<ClassificationLab> labs, bool symptomatic, bool epiLink)
    {
        var positive = labs.Where(IsPositive).ToList();

        if (positive.Any(lab => Has(lab.TestType, "PCR", "amplification", "Antigen")))
        {
            return (CaseStatusRules.ConfirmedCode, "COVID-19: a positive PCR or antigen test confirms the case.");
        }

        if (epiLink && positive.Any(lab => Has(lab.TestType, "IgG")))
        {
            return (CaseStatusRules.ProbableCode, "COVID-19: a positive IgG antibody with an epidemiological link makes the case probable.");
        }

        if (positive.Count > 0 && !symptomatic && !epiLink)
        {
            return (CaseStatusRules.NotACaseCode, "COVID-19: a positive test with no symptoms and no epidemiological link is not a case.");
        }

        return (null, "COVID-19: no lab classifies the case.");
    }

    private static Disease DiseaseOf(string? conditionName) => conditionName?.Trim() switch
    {
        { } name when Starts(name, "Chlamydia trachomatis") => Disease.Chlamydia,
        { } name when Starts(name, "Gonorrhea") => Disease.Gonorrhea,
        { } name when Starts(name, "Hepatitis C") => Disease.HepatitisC,
        { } name when Starts(name, "Influenza") => Disease.Influenza,
        { } name when Starts(name, "Syphilis") => Disease.Syphilis,
        { } name when Starts(name, "Measles") => Disease.Measles,
        { } name when Has(name, "COVID-19", "SARS-CoV-2") => Disease.Covid,
        _ => Disease.Other,
    };

    /// <summary>
    /// Whether a result is positive: its coded result begins Positive,
    /// Reactive or Detected — so Non-reactive and Not detected are not.
    /// </summary>
    private static bool IsPositive(ClassificationLab lab) =>
        lab.TestResult?.Trim() is { } result
        && (Starts(result, "Pos") || Starts(result, "Reactive") || Starts(result, "Detected"));

    private static bool IsGramStain(ClassificationLab lab) => Has(lab.TestType, "Gram stain");

    private static bool Names(string? organism, string name) => organism is not null && Starts(organism.Trim(), name);

    private static bool Starts(string value, string prefix) => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    private static bool Has(string? value, params string[] parts) =>
        value is not null && parts.Any(part => value.Contains(part, StringComparison.OrdinalIgnoreCase));
}
