using Survsys.Api;
using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// Which RR conditions this jurisdiction keeps, and where a kept one goes:
/// each determination code, another agency and no agency.
/// </summary>
public class EcrReportabilityTests
{
    private const string Agency = "2.16.840.1.113883.4.6^12341234";

    private static readonly string[] Agencies = [Agency];

    [Theory]
    [InlineData(EcrReportability.Reportable, true)]
    [InlineData(EcrReportability.MayBeReportable, true)]
    [InlineData(EcrReportability.NotReportable, false)]
    [InlineData(EcrReportability.NoRuleMet, false)]
    [InlineData(null, false)]
    public void IsKeptKeepsOnlyReportableAndMayBeReportable(string? determination, bool kept)
    {
        Assert.Equal(kept, EcrReportability.IsKept(Condition(determination, Agency), Agencies));
    }

    [Fact]
    public void IsKeptKeepsNothingForAnotherAgencyOrNone()
    {
        Assert.False(EcrReportability.IsKept(Condition(EcrReportability.Reportable, "2.16.840.1.113883.4.6^999"), Agencies));
        Assert.False(EcrReportability.IsKept(Condition(EcrReportability.Reportable, null), Agencies));
        Assert.False(EcrReportability.IsKept(Condition(EcrReportability.Reportable, Agency), []));
    }

    [Theory]
    [InlineData(EcrReportability.Reportable, true, EcrReportability.ReportableRouting)]
    [InlineData(EcrReportability.Reportable, false, EcrReportability.ReviewRouting)]
    [InlineData(EcrReportability.MayBeReportable, true, EcrReportability.ReviewRouting)]
    [InlineData(EcrReportability.MayBeReportable, false, EcrReportability.ReviewRouting)]
    public void RouteSendsOnlyAMappedReportableConditionOnAsReportable(string determination, bool mapped, string routing)
    {
        Assert.Equal(routing, EcrReportability.Route(determination, mapped));
    }

    [Fact]
    public void KeptAnswersTheKeptConditionsAsRows()
    {
        var rr = new CdaParser().Read(CdaParserTests.ReadFixture(CdaParserTests.RrR11));

        var kept = Assert.Single(EcrReportability.Kept(rr, Agencies));

        Assert.Equal("3928002", kept.Code);
        Assert.Equal(EcrReportability.Reportable, kept.DeterminationCode);
        Assert.Equal(Agency, kept.ResponsibleAgencyId);
        Assert.Empty(EcrReportability.Kept(rr, []));
    }

    private static RrCondition Condition(string? determination, string? agency) =>
        new(
            new CdaCode("3928002", "2.16.840.1.113883.6.96", "Zika virus disease (disorder)"),
            determination is null ? null : new CdaCode(determination, "2.16.840.1.114222.4.5.274", null),
            null,
            agency,
            null,
            null);
}
