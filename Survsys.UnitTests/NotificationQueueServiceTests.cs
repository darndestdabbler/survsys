using Microsoft.Extensions.Configuration;
using Survsys.Api;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The export queue worker's configuration: the MSH-11 code of each processing
/// id, and the options as they bind. The pass that reads and writes the
/// database is the integration suite's.
/// </summary>
public class NotificationQueueServiceTests
{
    [Theory]
    [InlineData(NotificationProcessing.Production, "P")]
    [InlineData(NotificationProcessing.Training, "T")]
    [InlineData(NotificationProcessing.Debug, "D")]
    public void ProcessingCodeIsTheMsh11Code(NotificationProcessing processing, string code) =>
        Assert.Equal(code, NotificationQueueService.ProcessingCode(processing));

    [Fact]
    public void AnEmptySectionLeavesTheWorkerOffOnTheProductionQueueKeepingTheLog()
    {
        var options = Bind([]);

        Assert.Null(options.PollIntervalSeconds);
        Assert.Equal(NotificationProcessing.Production, options.ProcessingId);
        Assert.Equal(NotificationQueueType.Production, options.Queue);
        Assert.Null(options.ExportLogRetentionDays);
    }

    [Fact]
    public void TheSectionBindsTheIntervalProcessingIdQueueAndRetention()
    {
        var options = Bind(new Dictionary<string, string?>
        {
            ["NotificationQueue:PollIntervalSeconds"] = "60",
            ["NotificationQueue:ProcessingId"] = "Training",
            ["NotificationQueue:Queue"] = "Onboarding",
            ["NotificationQueue:ExportLogRetentionDays"] = "730",
        });

        Assert.Equal(60, options.PollIntervalSeconds);
        Assert.Equal(NotificationProcessing.Training, options.ProcessingId);
        Assert.Equal(NotificationQueueType.Onboarding, options.Queue);
        Assert.Equal(730, options.ExportLogRetentionDays);
    }

    private static NotificationQueueOptions Bind(Dictionary<string, string?> values)
    {
        var options = new NotificationQueueOptions();
        new ConfigurationBuilder().AddInMemoryCollection(values).Build()
            .GetSection(NotificationQueueOptions.SectionName)
            .Bind(options);
        return options;
    }
}
