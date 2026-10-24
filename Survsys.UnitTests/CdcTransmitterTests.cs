using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Survsys.Api;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The CDC transmission's configuration: the options as they bind, and each
/// transport left off when its destination is empty. Transmitting to a stub
/// endpoint and writing to a folder are the integration suite's.
/// </summary>
public class CdcTransmitterTests
{
    [Fact]
    public void AnEmptySectionLeavesTheHttpTransportWithNoDestinationOrCredentials()
    {
        var options = Bind([]);

        Assert.Equal(CdcTransport.Http, options.Transport);
        Assert.Null(options.Endpoint);
        Assert.Null(options.OutboxDirectory);
        Assert.Null(options.UserName);
        Assert.Null(options.Password);
        Assert.Equal(30, options.TimeoutSeconds);
    }

    [Fact]
    public void TheSectionBindsTheTransportOutboxAndCredentials()
    {
        var options = Bind(new Dictionary<string, string?>
        {
            ["CdcTransmission:Transport"] = "File",
            ["CdcTransmission:OutboxDirectory"] = @"D:\phinms\outgoing",
            ["CdcTransmission:UserName"] = "survsys",
            ["CdcTransmission:Password"] = "from-the-environment",
        });

        Assert.Equal(CdcTransport.File, options.Transport);
        Assert.Equal(@"D:\phinms\outgoing", options.OutboxDirectory);
        Assert.Equal("survsys", options.UserName);
        Assert.Equal("from-the-environment", options.Password);
    }

    [Theory]
    [InlineData(CdcTransport.Http)]
    [InlineData(CdcTransport.File)]
    public async Task TransmitFaultsWhenTheTransportHasNoDestination(CdcTransport transport)
    {
        var transmitter = new CdcTransmitter(
            new HttpClient(),
            Options.Create(new CdcTransmissionOptions { Transport = transport }),
            NullLogger<CdcTransmitter>.Instance);

        var result = await transmitter.Transmit("MSH|^~\\&|");

        Assert.Equal(CdcTransmissionStatus.Faulted, result.Status);
        Assert.Contains("off", result.Detail, StringComparison.Ordinal);
        Assert.Null(result.BatchFile);
    }

    private static CdcTransmissionOptions Bind(Dictionary<string, string?> values)
    {
        var options = new CdcTransmissionOptions();
        new ConfigurationBuilder().AddInMemoryCollection(values).Build()
            .GetSection(CdcTransmissionOptions.SectionName)
            .Bind(options);
        return options;
    }
}
