using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The Connecticut profile against one fully conforming CT blood-lead message
/// for a child, shaped on the guide's examples: the message passes every rule,
/// and for each rule a variant breaking that rule alone fails it with the
/// element and severity the rule's usage gives.
/// </summary>
public class ConnecticutElrProfileTests
{
    private const string TestingApplicationOid = "2.16.840.1.113883.3.5609.4.1.1.3.2.2";
    private const string FacilityOid = "2.16.840.1.113883.3.5609.4.1";

    private static readonly string Conforming = string.Join('\r',
        "MSH|^~\\&|HealthSentry^2.16.840.1.113883.3.13.2.2.1^ISO|Lab^07D0092913^CLIA|CT^" + TestingApplicationOid
            + "^ISO|CTA-DPH^" + FacilityOid + "^ISO|20151004154300-0500||ORU^R01^ORU_R01|MSG1|T|2.5.1",
        "PID|1||999QQQ1234z^^^Lab&07D0092913&CLIA^MR||Patient^Test||20100115|F|||410 Capitol Avenue^^Hartford^CT^06106^USA^H",
        "NK1|1|Patient^Mother|MTH^Mother^HL70063|410 Capitol Avenue^^Hartford^CT^06106^USA^H",
        "ORC|RE|PL1^EHR^07D0092913^CLIA|FL1^EHR^2.16.840.1.113883.19.3.2.3^ISO|||||||||^Anydoctor^Adam||^WPN^PH^^^860^9995661||||||||||100 Main St^^New Britain^CT^06051",
        "OBR|1|PL2^EHR^07D0092913^CLIA|FL2^EHR^07D0092913^CLIA|5671-3^Lead BldC^LN|||20151003061900-0500|||||||||^Anydoctor^Adam|^WPN^PH^^^860^9995661",
        "OBX|1|NM|5671-3^Lead [Mass/volume] in Blood^LN|1|12|ug/dL|||||F",
        "SPM|1|SP1^SP1&EHR&07D0092913&CLIA||119297000^Blood^SCT");

    private static readonly ConformanceProfile Profile =
        ConnecticutElrProfile.Create(TestingApplicationOid, FacilityOid);

    private static string With(string segment, int field, string value) => Edit(Conforming, segment, field, value);

    /// <summary>
    /// <paramref name="message"/> with one field of <paramref name="segment"/>
    /// replaced; MSH's fields are numbered from the field separator.
    /// </summary>
    private static string Edit(string message, string segment, int field, string value) =>
        string.Join('\r', message.Split('\r').Select(line =>
        {
            if (!line.StartsWith(segment + "|", StringComparison.Ordinal))
            {
                return line;
            }

            var fields = line.Split('|').ToList();
            var index = segment == "MSH" ? field - 1 : field;
            while (fields.Count <= index)
            {
                fields.Add(string.Empty);
            }

            fields[index] = value;
            return string.Join('|', fields);
        }));

    private static string Without(string segment) =>
        string.Join('\r', Conforming.Split('\r').Where(line => !line.StartsWith(segment + "|", StringComparison.Ordinal)));

    public static TheoryData<string, string, int, string, ConformanceSeverity> Breaks => new()
    {
        { "MSH-5.2", "MSH", 5, "CT^2.16.840.1.113883.3.5609.4.1.1.3.2.1^ISO", ConformanceSeverity.Exception },
        { "MSH-6.2", "MSH", 6, "CTA-DPH^1.2.3^ISO", ConformanceSeverity.Exception },
        { "PID-3.4", "PID", 3, "999QQQ1234z^^^Lab&07D0092913&ISO^MR", ConformanceSeverity.Exception },
        { "PID-3.4", "PID", 3, "999QQQ1234z^^^^MR", ConformanceSeverity.Exception },
        { "PID-11", "PID", 11, "410 Capitol Avenue^^Gotham^CT^06106^USA^H", ConformanceSeverity.Warning },
        { "ORC-2", "ORC", 2, "PL1^EHR^LAB123^L", ConformanceSeverity.Exception },
        { "ORC-3", "ORC", 3, "FL1^EHR^2.16.840.1.113883.19.3.2.3^CLIA", ConformanceSeverity.Exception },
        { "ORC-24", "ORC", 24, "", ConformanceSeverity.Exception },
        { "OBR-2", "OBR", 2, "", ConformanceSeverity.Exception },
        { "OBR-3", "OBR", 3, "FL2^EHR^7D0092913^CLIA", ConformanceSeverity.Exception },
        { "OBR-16.2", "OBR", 16, "^^Adam", ConformanceSeverity.Exception },
        { "OBR-16.3", "OBR", 16, "^Anydoctor", ConformanceSeverity.Exception },
        { "OBR-17", "OBR", 17, "", ConformanceSeverity.Exception },
        { "OBX-2", "OBX", 2, "", ConformanceSeverity.Exception },
        { "SPM-2.2", "SPM", 2, "SP1^SP1&EHR&&", ConformanceSeverity.Exception },
    };

    [Fact]
    public void CreatePassesAFullyConformingMessage()
    {
        Assert.Empty(Profile.Check(Conforming));
    }

    [Theory]
    [MemberData(nameof(Breaks))]
    public void CreateFailsAMessageBreakingOneRule(
        string element, string segment, int field, string value, ConformanceSeverity severity)
    {
        Assert.DoesNotContain(Profile.Check(Conforming), failure => failure.Element == element);

        var failure = Assert.Single(Profile.Check(With(segment, field, value)));
        Assert.Equal(element, failure.Element);
        Assert.Equal(severity, failure.Severity);
    }

    [Fact]
    public void CreateExpectsTheReceivingApplicationOidItIsGiven()
    {
        var production = ConnecticutElrProfile.Create("2.16.840.1.113883.3.5609.4.1.1.3.2.1", FacilityOid);

        Assert.Equal("MSH-5.2", Assert.Single(production.Check(Conforming)).Element);
    }

    [Fact]
    public void CreateRequiresNextOfKinOnAChildsBloodLeadResult()
    {
        var failure = Assert.Single(Profile.Check(Without("NK1")));

        Assert.Equal("NK1", failure.Element);
        Assert.Equal(ConformanceSeverity.Exception, failure.Severity);
    }

    [Fact]
    public void CreateDoesNotRequireNextOfKinForAnAdultOrAnotherTest()
    {
        var withoutKin = Without("NK1");

        Assert.Empty(Profile.Check(Edit(withoutKin, "PID", 7, "19800115")));
        Assert.Empty(Profile.Check(Edit(withoutKin, "OBX", 3, "22327-1^Hepatitis C Antibody^LN")));
    }

    [Fact]
    public void CreatePassesATownOutsideConnecticutAndATownInAnyCase()
    {
        Assert.Empty(Profile.Check(With("PID", 11, "1 Main St^^Gotham^NY^10001^USA^H")));
        Assert.Empty(Profile.Check(With("PID", 11, "1 Main St^^NEW BRITAIN^CT^06051^USA^H")));
    }

    [Fact]
    public void TownsIsTheGuidesOneHundredSixtyNineTowns()
    {
        Assert.Equal(169, ConnecticutElrProfile.Towns.Count);
        Assert.Contains("North Stonington", ConnecticutElrProfile.Towns);
    }
}
