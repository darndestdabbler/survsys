using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the eCR endpoints make of a stored document and a parsed RR: the queue
/// summary with its finding and review counts, and the RR's conditions as the
/// document screen shows them, mapped and routed.
/// </summary>
/// <remarks>
/// No database and no host: that the queue pages and filters by feed, and that
/// the detail pairs an eICR with its RRs, are <c>Survsys.IntegrationTests</c>'s
/// to prove against a real SQL Server.
/// </remarks>
public class EcrEndpointsTests
{
    private const string Snomed = "2.16.840.1.113883.6.96";

    private const string SampleAgency = "2.16.840.1.113883.4.6^12341234";

    private static readonly IQueryable<EcrConditionMapping> NoMappings =
        Array.Empty<EcrConditionMapping>().AsQueryable();

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

        var response = EcrEndpoints.DocumentToResponse(NoMappings).Compile().Invoke(document);

        Assert.Equal(4, response.Id);
        Assert.Equal(EcrDocument.EicrKind, response.Kind);
        Assert.Equal(EcrDocument.TestFeed, response.Feed);
        Assert.Equal("2.16.840.1.113883.9.9.9^abc", response.DocumentId);
        Assert.Equal(2, response.VersionNumber);
        Assert.Equal("eicr.xml", response.Source);
        Assert.Equal(document.ReceivedAt, response.ReceivedAt);
        Assert.Equal(2, response.ErrorCount);
        Assert.Equal(3, response.WarningCount);
        Assert.Equal(0, response.ReviewCount);
    }

    [Fact]
    public void DocumentToResponseCountsTheKeptConditionsInReview()
    {
        var mappings = new[]
        {
            new EcrConditionMapping { CodeSystem = Snomed, Code = "mapped", ConditionId = 7 },
            new EcrConditionMapping { CodeSystem = Snomed, Code = "listed-unmapped", ConditionId = null },
        }.AsQueryable();
        var rr = new EcrDocument
        {
            Kind = EcrDocument.RrKind,
            RrConditions =
            [
                Kept("mapped", EcrReportability.Reportable),
                Kept("mapped", EcrReportability.MayBeReportable),
                Kept("listed-unmapped", EcrReportability.Reportable),
                Kept("absent", EcrReportability.Reportable),
            ],
        };

        var response = EcrEndpoints.DocumentToResponse(mappings).Compile().Invoke(rr);

        Assert.Equal(3, response.ReviewCount);
        Assert.False(EcrEndpoints.InReview(mappings).Compile().Invoke(rr.RrConditions[0]));
        Assert.All(rr.RrConditions.Skip(1), condition =>
            Assert.True(EcrEndpoints.InReview(mappings).Compile().Invoke(condition)));
    }

    [Fact]
    public void ConditionsCarryEachRrConditionsDeterminationAndAgency()
    {
        var rr = new CdaParser().Read(CdaParserTests.ReadFixture(CdaParserTests.RrR11));

        var condition = Assert.Single(EcrEndpoints.Conditions(rr, [], []));

        Assert.Equal("18218889-5d7d-47d4-b329-31bb1c28d3b3", condition.RrDocumentId);
        Assert.Equal("3928002", condition.ConditionCode);
        Assert.Equal("Zika virus disease (disorder)", condition.ConditionName);
        Assert.Equal("RRVS1", condition.DeterminationCode);
        Assert.Equal("RRVS7", condition.LocationCode);
        Assert.Equal("2.16.840.1.113883.4.6^12341234", condition.ResponsibleAgencyId);
        Assert.Equal("State Department of Health", condition.ResponsibleAgencyName);
        Assert.Equal("24 h", condition.ReportingTimeframe);
        Assert.Null(condition.MappedConditionId);
        Assert.Null(condition.Routing);
    }

    [Fact]
    public void ConditionsAreMappedAndRoutedForThisJurisdiction()
    {
        var rr = new CdaParser().Read(CdaParserTests.ReadFixture(CdaParserTests.RrR11));
        EcrConditionMappingResponse[] zika = [new(1, Snomed, "3928002", null, 12, "Zika virus")];

        var mapped = Assert.Single(EcrEndpoints.Conditions(rr, [SampleAgency], zika));
        var unmapped = Assert.Single(EcrEndpoints.Conditions(rr, [SampleAgency], []));

        Assert.Equal(12, mapped.MappedConditionId);
        Assert.Equal("Zika virus", mapped.MappedConditionName);
        Assert.Equal(EcrReportability.ReportableRouting, mapped.Routing);
        Assert.Null(unmapped.MappedConditionName);
        Assert.Equal(EcrReportability.ReviewRouting, unmapped.Routing);
    }

    [Fact]
    public void ConditionsOfAnEicrAreNone()
    {
        var eicr = new CdaParser().Read(CdaParserTests.ReadFixture(CdaParserTests.EicrR11));

        Assert.Empty(EcrEndpoints.Conditions(eicr, [SampleAgency], []));
    }

    private static EcrRrCondition Kept(string code, string determination) => new()
    {
        CodeSystem = Snomed,
        Code = code,
        DeterminationCode = determination,
        ResponsibleAgencyId = SampleAgency,
    };

    private static EcrValidationFinding Finding(string severity) => new()
    {
        Source = CdaValidator.SchematronSource,
        Severity = severity,
        Message = "SHALL contain exactly one [1..1] id.",
    };
}
