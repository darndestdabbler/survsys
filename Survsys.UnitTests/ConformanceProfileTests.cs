using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The conformance engine: each usage code, each conditional code with its
/// predicate both ways, and the severity a failed value check takes from its
/// usage.
/// </summary>
public class ConformanceProfileTests
{
    private const string Message =
        "MSH|^~\\&|APP|LAB|CT|DPH|20151004||ORU^R01|1|P|2.5.1\r"
        + "PID|1||A1^^^Lab&07D0092913&CLIA||Doe^Jane\r"
        + "OBX|1|NM|5671-3^Lead^LN";

    private static IReadOnlyList<ConformanceFailure> Check(ConformanceRule rule) =>
        new ConformanceProfile("test", [rule]).Check(Message);

    private static bool Always(Er7Message message) => true;

    private static bool Never(Er7Message message) => false;

    [Fact]
    public void CheckHoldsAnEmptyRequiredElementAsAnException()
    {
        var failure = Assert.Single(Check(ConformanceRule.Of("PID-7", Usage.R)));

        Assert.Equal("PID-7", failure.Element);
        Assert.Equal(ConformanceSeverity.Exception, failure.Severity);
        Assert.Empty(Check(ConformanceRule.Of("PID-5", Usage.R)));
    }

    [Fact]
    public void CheckHoldsARequiredElementOfAnAbsentSegmentAsAnException()
    {
        var failure = Assert.Single(Check(ConformanceRule.Of("NK1", Usage.R)));

        Assert.Equal(ConformanceSeverity.Exception, failure.Severity);
    }

    [Fact]
    public void CheckWarnsOfAnEmptyRequiredOrEmptyElement()
    {
        Assert.Equal(ConformanceSeverity.Warning, Assert.Single(Check(ConformanceRule.Of("PID-7", Usage.RE))).Severity);
        Assert.Empty(Check(ConformanceRule.Of("PID-5", Usage.RE)));
    }

    [Fact]
    public void CheckPassesAnEmptyOptionalElement()
    {
        Assert.Empty(Check(ConformanceRule.Of("PID-7", Usage.O)));
    }

    [Fact]
    public void CheckWarnsOfAValuedNotSupportedElement()
    {
        Assert.Equal(ConformanceSeverity.Warning, Assert.Single(Check(ConformanceRule.Of("PID-5", Usage.X))).Severity);
        Assert.Empty(Check(ConformanceRule.Of("PID-7", Usage.X)));
    }

    [Fact]
    public void CheckTakesAFailedValueChecksSeverityFromTheUsage()
    {
        static bool IsIso(string value) => value == "ISO";

        var required = Assert.Single(Check(ConformanceRule.Of("PID-3.4.3", Usage.R, IsIso, "ISO")));
        Assert.Equal(ConformanceSeverity.Exception, required.Severity);
        Assert.Contains("'CLIA' is not ISO", required.Detail);

        Assert.Equal(
            ConformanceSeverity.Warning,
            Assert.Single(Check(ConformanceRule.Of("PID-3.4.3", Usage.RE, IsIso, "ISO"))).Severity);
        Assert.Empty(Check(ConformanceRule.Of("PID-3.4.3", Usage.R, value => value == "CLIA", "CLIA")));
    }

    [Fact]
    public void ConditionalRequiredElseNotSupportedFollowsItsPredicate()
    {
        Assert.Equal(
            ConformanceSeverity.Exception,
            Assert.Single(Check(ConformanceRule.Conditional("PID-7", ConditionalUsage.RequiredElseNotSupported, Always))).Severity);
        Assert.Equal(
            ConformanceSeverity.Warning,
            Assert.Single(Check(ConformanceRule.Conditional("PID-5", ConditionalUsage.RequiredElseNotSupported, Never))).Severity);
    }

    [Fact]
    public void ConditionalRequiredOrEmptyElseNotSupportedFollowsItsPredicate()
    {
        Assert.Equal(
            ConformanceSeverity.Warning,
            Assert.Single(Check(ConformanceRule.Conditional("PID-7", ConditionalUsage.RequiredOrEmptyElseNotSupported, Always))).Severity);
        Assert.Empty(Check(ConformanceRule.Conditional("PID-7", ConditionalUsage.RequiredOrEmptyElseNotSupported, Never)));
        Assert.Single(Check(ConformanceRule.Conditional("PID-5", ConditionalUsage.RequiredOrEmptyElseNotSupported, Never)));
    }

    [Fact]
    public void ConditionalRequiredElseRequiredOrEmptyFollowsItsPredicate()
    {
        Assert.Equal(
            ConformanceSeverity.Exception,
            Assert.Single(Check(ConformanceRule.Conditional("PID-7", ConditionalUsage.RequiredElseRequiredOrEmpty, Always))).Severity);
        Assert.Equal(
            ConformanceSeverity.Warning,
            Assert.Single(Check(ConformanceRule.Conditional("PID-7", ConditionalUsage.RequiredElseRequiredOrEmpty, Never))).Severity);
    }

    [Fact]
    public void ConditionalRequiredElseOptionalFollowsItsPredicate()
    {
        Assert.Equal(
            ConformanceSeverity.Exception,
            Assert.Single(Check(ConformanceRule.Conditional("NK1", ConditionalUsage.RequiredElseOptional, Always))).Severity);
        Assert.Empty(Check(ConformanceRule.Conditional("NK1", ConditionalUsage.RequiredElseOptional, Never)));
    }

    [Fact]
    public void CheckReportsFailuresInTheOrderOfTheRules()
    {
        var profile = new ConformanceProfile("test", [ConformanceRule.Of("PID-7", Usage.R), ConformanceRule.Of("PID-8", Usage.RE)]);

        Assert.Equal(["PID-7", "PID-8"], profile.Check(Message).Select(failure => failure.Element));
    }
}
