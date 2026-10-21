using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The classification rules a case runs through on save, one case for each
/// rule <c>../nedss/docs/business-logic/CLASSIFICATION_RULES.md</c> gives —
/// contact closure, out-of-state status, jurisdiction from the zip, and the
/// chlamydia, gonorrhea, hepatitis C, influenza, syphilis, measles and
/// COVID-19 rules, every row of the COVID matrix among them.
/// </summary>
public class ClassificationRulesTests
{
    private const string Chlamydia = "Chlamydia trachomatis infection";
    private const string Gonorrhea = "Gonorrhea";
    private const string Covid = "Coronavirus disease 2019 (COVID-19)";

    private static ClassificationInput Case(
        string condition,
        params ClassificationLab[] labs) =>
        new(condition, EventTypes.Morbidity, WorkflowRules.AssignedToLhdCode, null, 1, "CT", null, labs);

    private static ClassificationLab Lab(string? organism, string testType, string result = "POSITIVE", bool approved = true) =>
        new(organism, testType, result, approved);

    [Fact]
    public void ClassifyConfirmsChlamydiaOnAPositiveApprovedLab()
    {
        var outcome = ClassificationRules.Classify(Case(Chlamydia, Lab("Chlamydia trachomatis", "PCR/amplification")));

        Assert.Equal(CaseStatusRules.ConfirmedCode, outcome.CaseStatus);
    }

    [Theory]
    [InlineData("Chlamydia trachomatis", "POSITIVE", false)]
    [InlineData("Chlamydia trachomatis", "Negative / Non-reactive", true)]
    [InlineData("Neisseria gonorrhoeae", "POSITIVE", true)]
    public void ClassifyReportsNoValidLabForChlamydiaWithoutOne(string organism, string result, bool approved)
    {
        var outcome = ClassificationRules.Classify(Case(Chlamydia, Lab(organism, "PCR/amplification", result, approved)));

        Assert.Null(outcome.CaseStatus);
        Assert.Contains(ClassificationRules.NoValidLab, outcome.Findings);
    }

    [Fact]
    public void ClassifyConfirmsGonorrheaOnAPositiveApprovedLabOtherThanAGramStain()
    {
        var outcome = ClassificationRules.Classify(Case(Gonorrhea,
            Lab("Neisseria gonorrhoeae", "Gram Stain"), Lab("Neisseria gonorrhoeae", "Culture")));

        Assert.Equal(CaseStatusRules.ConfirmedCode, outcome.CaseStatus);
    }

    [Fact]
    public void ClassifyMakesGonorrheaProbableOnAPositiveGramStainAlone()
    {
        var outcome = ClassificationRules.Classify(Case(Gonorrhea, Lab("Neisseria gonorrhoeae", "Gram Stain")));

        Assert.Equal(CaseStatusRules.ProbableCode, outcome.CaseStatus);
    }

    [Fact]
    public void ClassifyLeavesGonorrheaUnclassifiedWithoutAPositiveLab()
    {
        var outcome = ClassificationRules.Classify(Case(Gonorrhea, Lab("Neisseria gonorrhoeae", "Culture", "Negative")));

        Assert.Null(outcome.CaseStatus);
    }

    [Theory]
    [InlineData("Hepatitis C, acute", "Hepatitis C genotype by PCR", "POSITIVE", CaseStatusRules.ConfirmedCode)]
    [InlineData("Hepatitis C virus infection, past or present", "Viral Load - Qualitative RT-PCR", "Detected", CaseStatusRules.ConfirmedCode)]
    [InlineData("Hepatitis C, acute", "IgG Antibody", "POSITIVE", null)]
    [InlineData("Hepatitis C, acute", "Hepatitis C genotype by PCR", "Not detected", null)]
    public void ClassifyConfirmsHepatitisCOnAPositiveVirusLab(string condition, string testType, string result, string? expected)
    {
        var outcome = ClassificationRules.Classify(Case(condition, Lab("Hepatitis C virus", testType, result)));

        Assert.Equal(expected, outcome.CaseStatus);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ClassifyReportsAnInfluenzaHospitalizationWithNoneRecorded(bool facilityEncounter, bool reported)
    {
        var outcome = ClassificationRules.Classify(
            Case("Influenza-associated hospitalization") with { FacilityEncounter = facilityEncounter });

        Assert.Null(outcome.CaseStatus);
        Assert.Equal(reported, outcome.Findings.Any(finding => finding.Contains("hospitalization", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("Syphilis, primary")]
    [InlineData("Measles (rubeola)")]
    public void ClassifyLeavesSyphilisAndMeaslesToTheInvestigation(string condition)
    {
        var outcome = ClassificationRules.Classify(Case(condition, Lab("Treponema pallidum", "VDRL (non-treponemal antibody test)")));

        Assert.Null(outcome.CaseStatus);
        Assert.Single(outcome.Findings);
    }

    /// <summary>The COVID-19 matrix of section 3.7, row by row, for a Connecticut patient.</summary>
    [Theory]
    [InlineData("PCR/amplification", "POSITIVE", false, false, CaseStatusRules.ConfirmedCode)]
    [InlineData("Antigen by EIA/ELISA", "POSITIVE", false, false, CaseStatusRules.ConfirmedCode)]
    [InlineData("IgG Antibody", "POSITIVE", false, true, CaseStatusRules.ProbableCode)]
    [InlineData("IgG Antibody", "POSITIVE", false, false, CaseStatusRules.NotACaseCode)]
    [InlineData("IgG Antibody", "POSITIVE", true, false, null)]
    public void ClassifyAppliesTheCovidMatrix(string testType, string result, bool symptomatic, bool epiLink, string? expected)
    {
        var outcome = ClassificationRules.Classify(
            Case(Covid, Lab("SARS-CoV-2", testType, result)) with { Symptomatic = symptomatic, EpiLink = epiLink });

        Assert.Equal(expected, outcome.CaseStatus);
    }

    [Fact]
    public void ClassifyConfirmsCovidOnARecentPositiveAntigenDespiteANegativePcr()
    {
        var outcome = ClassificationRules.Classify(Case(Covid,
            Lab("SARS-CoV-2", "PCR/amplification", "Negative"), Lab("SARS-CoV-2", "Antigen by EIA/ELISA")));

        Assert.Equal(CaseStatusRules.ConfirmedCode, outcome.CaseStatus);
    }

    [Theory]
    [InlineData(Covid)]
    [InlineData(Chlamydia)]
    public void ClassifyMakesAnOutOfStatePatientsCaseOutOfState(string condition)
    {
        var outcome = ClassificationRules.Classify(
            Case(condition, Lab("SARS-CoV-2", "PCR/amplification")) with { PatientState = "Utah" });

        Assert.Equal(CaseStatusRules.OutOfStateCode, outcome.CaseStatus);
    }

    [Fact]
    public void ClassifyReclassifiesACaseNoLongerOutOfState()
    {
        var outcome = ClassificationRules.Classify(
            Case(Chlamydia, Lab("Chlamydia trachomatis", "Culture")) with { CaseStatus = CaseStatusRules.OutOfStateCode });

        Assert.Equal(CaseStatusRules.ConfirmedCode, outcome.CaseStatus);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("NV")]
    public void ClassifyNeverOverwritesAStatusAPersonSet(string? patientState)
    {
        var outcome = ClassificationRules.Classify(
            Case(Chlamydia, Lab("Chlamydia trachomatis", "Culture"))
                with { CaseStatus = CaseStatusRules.NotACaseCode, PatientState = patientState });

        Assert.Equal(CaseStatusRules.NotACaseCode, outcome.CaseStatus);
    }

    [Theory]
    [InlineData(null, 7, 7)]
    [InlineData(3, 7, 3)]
    [InlineData(null, null, null)]
    public void ClassifyTakesTheJurisdictionFromThePatientsZipWhereTheCaseHasNone(int? agency, int? zipAgency, int? expected)
    {
        var outcome = ClassificationRules.Classify(
            Case("Lyme disease") with { AgencyId = agency, ZipAgencyId = zipAgency });

        Assert.Equal(expected, outcome.AgencyId);
    }

    [Fact]
    public void ClassifyLeavesAnOutOfStateCasesAgencyAlone()
    {
        var outcome = ClassificationRules.Classify(
            Case("Lyme disease") with { AgencyId = null, ZipAgencyId = 7, PatientState = "NY" });

        Assert.Null(outcome.AgencyId);
    }

    [Theory]
    [InlineData(Chlamydia, EventTypes.Contact, WorkflowRules.AcceptedCode, true)]
    [InlineData(Gonorrhea, EventTypes.Contact, WorkflowRules.AcceptedCode, true)]
    [InlineData("Syphilis, secondary", EventTypes.Contact, WorkflowRules.AcceptedCode, true)]
    [InlineData(Chlamydia, EventTypes.Contact, WorkflowRules.AssignedToLhdCode, false)]
    [InlineData(Chlamydia, EventTypes.Morbidity, WorkflowRules.AcceptedCode, false)]
    [InlineData("Measles (rubeola)", EventTypes.Contact, WorkflowRules.AcceptedCode, false)]
    public void ClassifyClosesAnAcceptedChlamydiaGonorrheaOrSyphilisContact(
        string condition, string eventType, string workflowState, bool expected)
    {
        var outcome = ClassificationRules.Classify(
            Case(condition) with { EventType = eventType, WorkflowState = workflowState });

        Assert.Equal(expected, outcome.CloseContact);
    }
}
