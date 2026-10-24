using System.Xml.Linq;
using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The CDA validator run over a valid and an invalid document of each version
/// read: the HL7 IG sample as published, which the guide's own schematron
/// passes with warnings at most, and the same sample with its required
/// ClinicalDocument/id taken out, which both the schema and the schematron
/// refuse.
/// </summary>
public class CdaValidatorTests
{
    private static readonly XNamespace V3 = "urn:hl7-org:v3";

    public static TheoryData<string, string, string> Versions => new()
    {
        { CdaParserTests.EicrR11, CdaParser.EicrKind, "R1.1" },
        { CdaParserTests.EicrR31, CdaParser.EicrKind, "R3.1" },
        { CdaParserTests.EicrR311, CdaParser.EicrKind, "R3.1.1" },
        { CdaParserTests.RrR11, CdaParser.RrKind, "1.1" },
    };

    [Theory]
    [MemberData(nameof(Versions))]
    public void TheIgSampleHasNoFatalOrErrorFinding(string fixture, string kind, string version)
    {
        var findings = new CdaValidator().Validate(CdaParserTests.ReadFixture(fixture), kind, version);

        Assert.DoesNotContain(findings, finding => finding.Severity != CdaValidator.Warning);
        Assert.Contains(findings, finding => finding.Source == CdaValidator.SchematronSource);
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void TheSampleWithoutItsIdHasASchemaErrorAndASchematronError(string fixture, string kind, string version)
    {
        var document = XDocument.Parse(CdaParserTests.ReadFixture(fixture));
        document.Root!.Element(V3 + "id")!.Remove();

        var findings = new CdaValidator().Validate(document.ToString(), kind, version);

        Assert.Contains(findings, finding =>
            finding.Source == CdaValidator.SchemaSource && finding.Severity == CdaValidator.Error);
        Assert.Contains(findings, finding =>
            finding.Source == CdaValidator.SchematronSource
            && finding.Severity is CdaValidator.Fatal or CdaValidator.Error
            && finding.Location is not null);
    }

    [Fact]
    public void ValidateRefusesAVersionWithNoSchematron()
    {
        Assert.Throws<ArgumentException>(() =>
            new CdaValidator().Validate(CdaParserTests.ReadFixture(CdaParserTests.RrR11), CdaParser.RrKind, "2.0"));
    }
}
