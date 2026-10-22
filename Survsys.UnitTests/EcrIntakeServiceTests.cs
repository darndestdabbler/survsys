using System.Xml.Linq;
using Survsys.Api;
using Survsys.Data;
using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The eCR intake's decisions, over the HL7 implementation guides' sample
/// documents: what is stored for a document from each feed, the validation
/// findings stored with it, a re-sent version grouped with its first, and an RR
/// paired with its eICR — with no host, no database and no folder.
/// </summary>
public class EcrIntakeServiceTests
{
    private const string R311Id = """<id root="2.16.840.1.113883.9.9.9.9.9" extension="db734647-fc99-424c-a864-7e3cda82e704" />""";

    private static readonly CdaParser Parser = new();

    private static readonly CdaValidator Validator = new();

    private static readonly string[] NoAgencies = [];

    /// <summary>The responsible agency the RR sample names.</summary>
    private const string SampleAgency = "2.16.840.1.113883.4.6^12341234";

    [Theory]
    [InlineData(EcrDocument.TestFeed)]
    [InlineData(EcrDocument.ProductionFeed)]
    public void StageKeepsTheRawXmlUnchangedWithTheFeedItCameFrom(string feed)
    {
        var raw = CdaParserTests.ReadFixture(CdaParserTests.EicrR311);

        var document = EcrIntakeService.Stage(Parser, Validator, raw, feed, "eicr.xml", NoAgencies);

        Assert.Same(raw, document.RawXml);
        Assert.Equal(feed, document.Feed);
        Assert.Equal("eicr.xml", document.Source);
        Assert.Equal(EcrDocument.EicrKind, document.Kind);
        Assert.Equal("2.16.840.1.113883.9.9.9.9.9^db734647-fc99-424c-a864-7e3cda82e704", document.DocumentId);
        Assert.Equal("2.16.840.1.113883.3.117.1.1.5.2.1.1.1^31", document.SetId);
        Assert.Equal(2, document.VersionNumber);
        Assert.Null(document.EicrDocumentId);
    }

    [Fact]
    public void StageRecordsTheEicrAnRrRespondsTo()
    {
        var document = EcrIntakeService.Stage(
            Parser, Validator, CdaParserTests.ReadFixture(CdaParserTests.RrR11), EcrDocument.ProductionFeed, null, NoAgencies);

        Assert.Equal(EcrDocument.RrKind, document.Kind);
        Assert.Equal("ca316e79-aaf2-4e8c-aa5f-848093bb8bb1", document.EicrDocumentId);
    }

    [Fact]
    public void StageKeepsTheRrConditionsThisJurisdictionIsResponsibleFor()
    {
        var rr = EcrIntakeService.Stage(
            Parser, Validator, CdaParserTests.ReadFixture(CdaParserTests.RrR11), EcrDocument.ProductionFeed, null, [SampleAgency]);
        var forAnother = EcrIntakeService.Stage(
            Parser, Validator, CdaParserTests.ReadFixture(CdaParserTests.RrR11), EcrDocument.ProductionFeed, null, NoAgencies);
        var eicr = EcrIntakeService.Stage(
            Parser, Validator, CdaParserTests.ReadFixture(CdaParserTests.EicrR11), EcrDocument.ProductionFeed, null, [SampleAgency]);

        var kept = Assert.Single(rr.RrConditions);
        Assert.Equal("3928002", kept.Code);
        Assert.Equal("2.16.840.1.113883.6.96", kept.CodeSystem);
        Assert.Equal("Zika virus disease (disorder)", kept.DisplayName);
        Assert.Equal(EcrReportability.Reportable, kept.DeterminationCode);
        Assert.Equal(SampleAgency, kept.ResponsibleAgencyId);
        Assert.Equal("24 h", kept.ReportingTimeframe);
        Assert.Empty(forAnother.RrConditions);
        Assert.Empty(eicr.RrConditions);
    }

    [Fact]
    public void StageRefusesAnUnreadableDocumentAndAnUnknownFeed()
    {
        Assert.Throws<Hl7ParseException>(
            () => EcrIntakeService.Stage(Parser, Validator, "not xml", EcrDocument.TestFeed, null, NoAgencies));
        Assert.Throws<ArgumentException>(
            () => EcrIntakeService.Stage(
                Parser, Validator, CdaParserTests.ReadFixture(CdaParserTests.EicrR11), "staging", null, NoAgencies));
    }

    [Fact]
    public void StageKeepsAnInvalidDocumentWithItsFindings()
    {
        var valid = EcrIntakeService.Stage(
            Parser, Validator, CdaParserTests.ReadFixture(CdaParserTests.EicrR11), EcrDocument.TestFeed, null, NoAgencies);
        // The parser needs a document's id, so the invalid document lacks the
        // effectiveTime that the schema and the schematron both require instead.
        var xml = XDocument.Parse(CdaParserTests.ReadFixture(CdaParserTests.EicrR11));
        xml.Root!.Element(XName.Get("effectiveTime", "urn:hl7-org:v3"))!.Remove();
        var withoutEffectiveTime = xml.ToString();
        var invalid = EcrIntakeService.Stage(Parser, Validator, withoutEffectiveTime, EcrDocument.TestFeed, null, NoAgencies);

        Assert.NotEmpty(valid.Findings);
        Assert.All(valid.Findings, finding => Assert.Equal(CdaValidator.Warning, finding.Severity));
        Assert.Same(withoutEffectiveTime, invalid.RawXml);
        Assert.Equal(valid.DocumentId, invalid.DocumentId);
        Assert.Contains(invalid.Findings, finding =>
            finding.Source == CdaValidator.SchemaSource && finding.Severity == CdaValidator.Error);
        Assert.Contains(invalid.Findings, finding =>
            finding.Source == CdaValidator.SchematronSource && finding.Severity != CdaValidator.Warning);
    }

    [Fact]
    public void AReSentVersionIsGroupedWithItsFirstInVersionOrder()
    {
        var sample = CdaParserTests.ReadFixture(CdaParserTests.EicrR311);
        var resent = ReplaceFirst(
            ReplaceFirst(sample, R311Id, """<id root="2.16.840.1.113883.9.9.9.9.9" extension="resent" />"""),
            """<versionNumber value="2" />""",
            """<versionNumber value="3" />""");

        var third = EcrIntakeService.Stage(Parser, Validator, resent, EcrDocument.ProductionFeed, null, NoAgencies);
        var second = EcrIntakeService.Stage(Parser, Validator, sample, EcrDocument.ProductionFeed, null, NoAgencies);
        var onTest = EcrIntakeService.Stage(Parser, Validator, sample, EcrDocument.TestFeed, null, NoAgencies);
        var other = EcrIntakeService.Stage(
            Parser, Validator, CdaParserTests.ReadFixture(CdaParserTests.EicrR11), EcrDocument.ProductionFeed, null, NoAgencies);

        var versions = EcrIntakeService
            .Versions(new[] { third, other, onTest, second }.AsQueryable(), EcrDocument.ProductionFeed, second.SetId!)
            .ToList();

        Assert.Equal([second, third], versions);
        Assert.Equal([2, 3], versions.Select(version => version.VersionNumber));
    }

    [Fact]
    public void AnRrIsPairedWithItsEicrWhicheverArrivesFirst()
    {
        var rr = EcrIntakeService.Stage(
            Parser, Validator, CdaParserTests.ReadFixture(CdaParserTests.RrR11), EcrDocument.ProductionFeed, null, NoAgencies);
        var eicr = EcrIntakeService.Stage(
            Parser, Validator,
            CdaParserTests.ReadFixture(CdaParserTests.EicrR11)
                .Replace("db734647-fc99-424c-a864-7e3cda82e703", "ca316e79-aaf2-4e8c-aa5f-848093bb8bb1"),
            EcrDocument.ProductionFeed,
            null, NoAgencies);
        var onTest = EcrIntakeService.Stage(
            Parser, Validator, CdaParserTests.ReadFixture(CdaParserTests.RrR11), EcrDocument.TestFeed, null, NoAgencies);

        foreach (var stored in new[] { new[] { rr, onTest, eicr }, new[] { eicr, onTest, rr } })
        {
            Assert.Same(eicr, Assert.Single(EcrIntakeService.EicrOf(stored.AsQueryable(), rr)));
            Assert.Same(rr, Assert.Single(EcrIntakeService.ResponsesTo(stored.AsQueryable(), eicr)));
        }

        Assert.Empty(EcrIntakeService.EicrOf(new[] { rr }.AsQueryable(), rr));
    }

    private static string ReplaceFirst(string text, string oldValue, string newValue)
    {
        var at = text.IndexOf(oldValue, StringComparison.Ordinal);
        Assert.True(at >= 0, $"The fixture no longer contains {oldValue}.");
        return string.Concat(text.AsSpan(0, at), newValue, text.AsSpan(at + oldValue.Length));
    }
}
