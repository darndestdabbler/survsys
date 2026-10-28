using Survsys.Api;
using Survsys.Contracts;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the report endpoint decides without the database: which requests are
/// a report it can run.
/// </summary>
public class ReportEndpointsTests
{
    [Fact]
    public void WhyNotAReportIsNullForEveryGroupingWithNoRange()
    {
        Assert.All(ReportGroupings.All, grouping => Assert.Null(ReportEndpoints.WhyNotAReport(grouping, null, null)));
    }

    [Fact]
    public void WhyNotAReportIsNullForARangeOfOneDay()
    {
        var day = new DateOnly(2026, 3, 8);

        Assert.Null(ReportEndpoints.WhyNotAReport(ReportGroupings.Condition, day, day));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("person")]
    [InlineData("Condition")]
    public void WhyNotAReportNamesTheGroupingsForOneItDoesNotKnow(string? groupBy)
    {
        Assert.Contains(ReportGroupings.CaseStatus, ReportEndpoints.WhyNotAReport(groupBy, null, null));
    }

    [Fact]
    public void WhyNotAReportRefusesARangeThatEndsBeforeItStarts()
    {
        Assert.NotNull(ReportEndpoints.WhyNotAReport(
            ReportGroupings.Agency, new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 8)));
    }
}
