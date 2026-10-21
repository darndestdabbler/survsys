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
    public void AnEmptySectionLeavesTheWorkerOffOnTheProductionQueue()
    {
        var options = Bind([]);

        Assert.Null(options.PollIntervalSeconds);
        Assert.Equal(NotificationProcessing.Production, options.ProcessingId);
        Assert.Equal(NotificationQueueType.Production, options.Queue);
    }

    [Fact]
    public void TheSectionBindsTheIntervalProcessingIdAndQueue()
    {
        var options = Bind(new Dictionary<string, string?>
        {
            ["NotificationQueue:PollIntervalSeconds"] = "60",
            ["NotificationQueue:ProcessingId"] = "Training",
            ["NotificationQueue:Queue"] = "Onboarding",
        });

        Assert.Equal(60, options.PollIntervalSeconds);
        Assert.Equal(NotificationProcessing.Training, options.ProcessingId);
        Assert.Equal(NotificationQueueType.Onboarding, options.Queue);
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
