using System.Xml.Linq;
using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The CDA parser read over the HL7 implementation guides' own sample
/// documents, unchanged: the eICR R1.1, R3.1 (the STU3 guide's sample, from
/// HL7/CDA-phcaserpt-1.3.0) and R3.1.1 samples and the RR 1.1 sample, each landed into the fields the eCR sessions read, and documents
/// that are not eCR refused.
/// </summary>
public class CdaParserTests
{
    public const string EicrR11 = "CDAR2_IG_PHCASERPT_R2_STU1.1_Sample.xml";
    public const string EicrR31 = "CDAR2_IG_PHCASERPT_R2_D3_SAMPLE.xml";
    public const string EicrR311 ="CDAR2_IG_PHCASERPT_R2_STU3.1.1_SAMPLE.xml";
    public const string RrR11 = "CDAR2_IG_PHCR_R2_RR_D1_2017DEC_SAMPLE.xml";

    public static string ReadFixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Cda", name));

    [Fact]
    public void ReadsTheEicrR11Sample()
    {
        var document = new CdaParser().Read(ReadFixture(EicrR11));

        Assert.Equal(CdaParser.EicrKind, document.Kind);
        Assert.Equal("R1.1", document.IgVersion);
        Assert.Equal("db734647-fc99-424c-a864-7e3cda82e703", document.DocumentId);
        Assert.Null(document.SetId);
        Assert.Null(document.VersionNumber);
        Assert.Equal("2.16.840.1.113883.4.6^88888888", document.Custodian);
        Assert.Null(document.EicrDocumentId);
        Assert.Empty(document.Conditions);

        var patient = document.Patient;
        Assert.Equal("Stinn", patient.Person.LastName);
        Assert.Equal("Jane", patient.Person.FirstName);
        Assert.Equal("H", patient.Person.MiddleName);
        Assert.Equal(new DateOnly(1974, 11, 24), patient.Person.BirthDate);
        Assert.Equal("F", patient.Person.BirthGender);
        Assert.Equal("5101 Peachtree St NE", patient.Street);
        Assert.Equal("Atlanta", patient.City);
        Assert.Equal("GA", patient.State);
        Assert.Equal("30302", patient.Zip);
        Assert.Equal("Fulton County", patient.County);

        var encounter = Assert.IsType<CdaEncounter>(document.Encounter);
        Assert.Equal("2.16.840.1.113883.19^9937012", encounter.Id);
        Assert.Equal("99213", encounter.Type?.Code);
        Assert.Equal(new DateOnly(2016, 11, 7), encounter.Start);
        Assert.Equal(new DateOnly(2016, 11, 7), encounter.End);
        Assert.Equal("2.16.840.1.113883.4.6^77777777777", encounter.FacilityId);
        Assert.Equal("Salem Medical Center", encounter.FacilityName);

        var trigger = Assert.Single(document.Problems, problem => problem.IsTrigger);
        Assert.Equal("27836007", trigger.Value?.Code);
        Assert.Equal("Pertussis (disorder)", trigger.Value?.DisplayName);
        Assert.Equal(new DateOnly(2016, 11, 7), trigger.Onset);
        Assert.Contains(document.Problems, problem => problem.Value?.Code == "43025008");

        var medication = Assert.Single(document.Medications);
        Assert.Equal("248656", medication.Drug?.Code);
        Assert.Equal(new DateOnly(2016, 11, 7), medication.Start);
        Assert.Equal("1 g", medication.Dose);
        Assert.Equal("ORAL", medication.Route);

        var pregnancy = Assert.IsType<CdaPregnancy>(document.Pregnancy);
        Assert.Equal("77386006", pregnancy.Status?.Code);
        Assert.True(pregnancy.Negated);
        Assert.Equal(new DateOnly(2016, 1, 1), pregnancy.From);
        Assert.Equal(new DateOnly(2016, 11, 7), pregnancy.To);

        Assert.Contains(document.Travel, travel => travel.Location == "Brazil" && travel.To == new DateOnly(2016, 11, 9));
        Assert.Contains(
            document.Travel,
            travel => travel.Text == "Spent 8 years in the UK during the BSE outbreak"
                && travel.From == new DateOnly(1999, 1, 1));

        var occupation = Assert.Single(document.Occupations);
        Assert.StartsWith("Centers for Disease Control", occupation.Text);
        Assert.Equal(new DateOnly(1993, 1, 1), occupation.From);

        var cbc = Assert.Single(patient.Orders, order => order.TestCode == "57021-8");
        Assert.Equal(new DateOnly(2016, 11, 7), cbc.ObservedOn);
        var lymphocytes = Assert.Single(cbc.Results, result => result.ObservationCode == "731-0");
        Assert.Equal("5.2", lymphocytes.ResultValue);
        Assert.Null(lymphocytes.CodedResult);
        Assert.Equal("10*3/uL", lymphocytes.Units);
        Assert.Equal("1.0-4.8", lymphocytes.ReferenceRange);
        Assert.Equal("H", lymphocytes.AbnormalFlag);
        Assert.Equal("completed", lymphocytes.ResultStatus);
    }

    [Fact]
    public void ReadsTheEicrR311Sample()
    {
        var document = new CdaParser().Read(ReadFixture(EicrR311));

        Assert.Equal(CdaParser.EicrKind, document.Kind);
        Assert.Equal("R3.1.1", document.IgVersion);
        Assert.Equal("2.16.840.1.113883.9.9.9.9.9^db734647-fc99-424c-a864-7e3cda82e704", document.DocumentId);
        Assert.Equal("2.16.840.1.113883.3.117.1.1.5.2.1.1.1^31", document.SetId);
        Assert.Equal(2, document.VersionNumber);

        var patient = document.Patient;
        Assert.Equal("Everywoman", patient.Person.LastName);
        Assert.Equal("Eve", patient.Person.FirstName);
        Assert.Equal("2222 Home Street", patient.Street);
        Assert.Equal("26001", patient.County);

        Assert.Equal("AMB", document.Encounter?.Type?.Code);
        Assert.Contains(document.Problems, problem => problem.IsTrigger);
        Assert.NotEmpty(document.Medications);
        Assert.NotEmpty(patient.Orders);
        Assert.All(patient.Orders, order => Assert.NotEmpty(order.Results));

        var pregnancy = Assert.IsType<CdaPregnancy>(document.Pregnancy);
        Assert.Equal("77386006", pregnancy.Status?.Code);
        Assert.False(pregnancy.Negated);

        Assert.Contains(document.Travel, travel => travel.Location == "FJ, Nadi");
        Assert.Contains(document.Occupations, occupation => occupation.Occupation?.Code == "3600" && occupation.Text == "Nurse");

        // The sample carries an RR's information in a section of its own; an
        // eICR's conditions are not read from it.
        Assert.Empty(document.Conditions);
    }

    [Fact]
    public void ReadsTheEicrR31Sample()
    {
        var document = new CdaParser().Read(ReadFixture(EicrR31));

        Assert.Equal(CdaParser.EicrKind, document.Kind);
        Assert.Equal("R3.1", document.IgVersion);
        Assert.Equal("2.16.840.1.113883.9.9.9.9.9^db734647-fc99-424c-a864-7e3cda82e704", document.DocumentId);
        Assert.Equal("2.16.840.1.113883.3.117.1.1.5.2.1.1.1^31", document.SetId);
        Assert.Equal(2, document.VersionNumber);

        var patient = document.Patient;
        Assert.Equal("Everywoman", patient.Person.LastName);
        Assert.Equal("Eve", patient.Person.FirstName);
        Assert.Equal("26001", patient.County);

        Assert.Equal("2.16.840.1.113883.19^9937012", document.Encounter?.Id);
        var trigger = Assert.Single(document.Problems, problem => problem.IsTrigger);
        Assert.Equal("27836007", trigger.Value?.Code);
        Assert.NotEmpty(document.Medications);
        Assert.NotEmpty(patient.Orders);
        Assert.NotNull(document.Pregnancy);
        Assert.NotEmpty(document.Travel);
        Assert.NotEmpty(document.Occupations);
        Assert.Empty(document.Conditions);
    }

    [Fact]
    public void ReadsTheRr11Sample()
    {
        var document = new CdaParser().Read(ReadFixture(RrR11));

        Assert.Equal(CdaParser.RrKind, document.Kind);
        Assert.Equal("1.1", document.IgVersion);
        Assert.Equal("18218889-5d7d-47d4-b329-31bb1c28d3b3", document.DocumentId);
        Assert.Equal("ca316e79-aaf2-4e8c-aa5f-848093bb8bb1", document.EicrDocumentId);
        Assert.Equal("Everywoman", document.Patient.Person.LastName);

        var condition = Assert.Single(document.Conditions);
        Assert.Equal("3928002", condition.Condition?.Code);
        Assert.Equal("Zika virus disease (disorder)", condition.Condition?.DisplayName);
        Assert.Equal("RRVS1", condition.Determination?.Code);
        Assert.Equal("RRVS7", condition.Location?.Code);
        Assert.Equal("2.16.840.1.113883.4.6^12341234", condition.ResponsibleAgencyId);
        Assert.Equal("State Department of Health", condition.ResponsibleAgencyName);
        Assert.Equal("24 h", condition.ReportingTimeframe);
    }

    [Fact]
    public void ParseMapsTheDocumentOntoTheIntakeSeam()
    {
        var parser = new CdaParser();
        var message = parser.Parse(ReadFixture(EicrR11));

        Assert.Equal(CdaParser.CdaFormat, parser.Format);
        Assert.Equal(CdaParser.CdaFormat, message.Format);
        Assert.Equal(CdaParser.EicrKind, message.MessageType);
        Assert.Equal("2.16.840.1.113883.4.6^88888888", message.SendingFacility);
        var patient = Assert.Single(message.Patients);
        Assert.Equal("Stinn", patient.Person.LastName);
        Assert.Contains(patient.Orders, order => order.TestCode == "57021-8");
    }

    [Fact]
    public void RefusesTextThatIsNotXml()
    {
        Assert.Throws<Hl7ParseException>(() => new CdaParser().Read("MSH|^~\\&|LAB"));
    }

    [Fact]
    public void RefusesADocumentThatIsNotAnEicrOrRr()
    {
        const string ccd = """
            <ClinicalDocument xmlns="urn:hl7-org:v3">
              <templateId root="2.16.840.1.113883.10.20.22.1.2" extension="2015-08-01" />
              <id root="1.2.3" />
            </ClinicalDocument>
            """;

        Assert.Throws<Hl7ParseException>(() => new CdaParser().Read(ccd));
    }

    [Fact]
    public void RefusesAnEicrWithNoId()
    {
        const string eicr = """
            <ClinicalDocument xmlns="urn:hl7-org:v3">
              <templateId root="2.16.840.1.113883.10.20.15.2" extension="2016-12-01" />
            </ClinicalDocument>
            """;

        Assert.Throws<Hl7ParseException>(() => new CdaParser().Read(eicr));
    }

    [Fact]
    public void RefusesADocumentTypeDeclaration()
    {
        const string withDtd = """
            <!DOCTYPE ClinicalDocument [<!ENTITY x "x">]>
            <ClinicalDocument xmlns="urn:hl7-org:v3" />
            """;

        Assert.Throws<Hl7ParseException>(() => new CdaParser().Read(withDtd));
    }

    [Fact]
    public void FormatIdJoinsRootAndExtension()
    {
        XNamespace v3 = "urn:hl7-org:v3";

        Assert.Equal("1.2.3^abc", CdaParser.FormatId(new XElement(v3 + "id", new XAttribute("root", "1.2.3"), new XAttribute("extension", "abc"))));
        Assert.Equal("1.2.3", CdaParser.FormatId(new XElement(v3 + "id", new XAttribute("root", "1.2.3"))));
        Assert.Null(CdaParser.FormatId(new XElement(v3 + "id", new XAttribute("nullFlavor", "NI"))));
        Assert.Null(CdaParser.FormatId(null));
    }
}
