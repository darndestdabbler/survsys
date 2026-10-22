using Survsys.Api;
using Survsys.Data;
using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the eCR endpoints make of a stored document and a parsed RR: the queue
/// summary with its finding counts, and the RR's conditions as the document
/// screen shows them.
/// </summary>
/// <remarks>
/// No database and no host: that the queue pages and filters by feed, and that
/// the detail pairs an eICR with its RRs, are <c>Survsys.IntegrationTests</c>'s
/// to prove against a real SQL Server.
/// </remarks>
public class EcrEndpointsTests
{
    [Fact]
    public void DocumentToResponseCarriesTheSummaryAndCountsErrorsAndWarnings()
    {
        var document = new EcrDocument
        {
            Id = 4,
            DocumentId = "2.16.840.1.113883.9.9.9^abc",
            Kind = EcrDocument.EicrKind,
            Feed = EcrDocument.TestFeed,
            VersionNumber = 2,
            RawXml = "<ClinicalDocument />",
            Source = "eicr.xml",
            ReceivedAt = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc),
            Findings =
            [
                Finding(CdaValidator.Fatal),
                Finding(CdaValidator.Error),
                Finding(CdaValidator.Warning),
                Finding(CdaValidator.Warning),
                Finding(CdaValidator.Warning),
            ],
        };

        var response = EcrEndpoints.DocumentToResponse.Compile().Invoke(document);

        Assert.Equal(4, response.Id);
        Assert.Equal(EcrDocument.EicrKind, response.Kind);
        Assert.Equal(EcrDocument.TestFeed, response.Feed);
        Assert.Equal("2.16.840.1.113883.9.9.9^abc", response.DocumentId);
        Assert.Equal(2, response.VersionNumber);
        Assert.Equal("eicr.xml", response.Source);
        Assert.Equal(document.ReceivedAt, response.ReceivedAt);
        Assert.Equal(2, response.ErrorCount);
        Assert.Equal(3, response.WarningCount);
    }

    [Fact]
    public void ConditionsCarryEachRrConditionsDeterminationAndAgency()
    {
        var rr = new CdaParser().Read(CdaParserTests.ReadFixture(CdaParserTests.RrR11));

        var condition = Assert.Single(EcrEndpoints.Conditions(rr));

        Assert.Equal("18218889-5d7d-47d4-b329-31bb1c28d3b3", condition.RrDocumentId);
        Assert.Equal("3928002", condition.ConditionCode);
        Assert.Equal("Zika virus disease (disorder)", condition.ConditionName);
        Assert.Equal("RRVS1", condition.DeterminationCode);
        Assert.Equal("RRVS7", condition.LocationCode);
        Assert.Equal("2.16.840.1.113883.4.6^12341234", condition.ResponsibleAgencyId);
        Assert.Equal("State Department of Health", condition.ResponsibleAgencyName);
        Assert.Equal("24 h", condition.ReportingTimeframe);
    }

    [Fact]
    public void ConditionsOfAnEicrAreNone()
    {
        var eicr = new CdaParser().Read(CdaParserTests.ReadFixture(CdaParserTests.EicrR11));

        Assert.Empty(EcrEndpoints.Conditions(eicr));
    }

    private static EcrValidationFinding Finding(string severity) => new()
    {
        Source = CdaValidator.SchematronSource,
        Severity = severity,
        Message = "SHALL contain exactly one [1..1] id.",
    };
}
