using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Survsys.Api;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// <see cref="CdcTransmitter"/> against a local stub endpoint rather than CDC:
/// a loopback host stands in for the receiving endpoint, so the transmission is
/// proved end to end — the POST, the body and the recorded outcome — without a
/// credential and without reaching CDC. The message transmitted is the
/// generated default-guide notification fixture, read as text, so the suite
/// needs neither <c>Survsys.Messaging</c> nor the database.
/// </summary>
public sealed class CdcTransmitterTests
{
    private static async Task<string> FixtureMessage() =>
        await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Hl7", "oru-r01-notification-chlamydia.hl7"));

    private static CdcTransmitter Transmitter(string endpoint, int timeoutSeconds) =>
        new(
            new HttpClient(),
            Options.Create(new CdcTransmissionOptions { Endpoint = endpoint, TimeoutSeconds = timeoutSeconds }),
            NullLogger<CdcTransmitter>.Instance);

    /// <summary>
    /// A loopback host that answers every request with <paramref name="handler"/>,
    /// bound to an OS-assigned port. The returned URL is where the transmitter
    /// is pointed; disposing the app stops it.
    /// </summary>
    private static async Task<(WebApplication App, string Url)> StartStub(RequestDelegate handler)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();

        var app = builder.Build();
        app.Urls.Clear();
        app.Urls.Add("http://127.0.0.1:0");
        app.Run(handler);
        await app.StartAsync();

        return (app, app.Urls.First());
    }

    [Fact]
    public async Task AnAcceptingStubRecordsAcceptedAndReceivesTheMessageUnchanged()
    {
        var message = await FixtureMessage();
        string? received = null;

        var (app, url) = await StartStub(async context =>
        {
            received = await new StreamReader(context.Request.Body).ReadToEndAsync();
            context.Response.StatusCode = StatusCodes.Status200OK;
        });

        try
        {
            var result = await Transmitter(url, timeoutSeconds: 30).Transmit(message);

            Assert.Equal(CdcTransmissionStatus.Accepted, result.Status);
            Assert.Equal(message, received);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task ARejectingStubRecordsRejectedWithTheStatusInTheDetail()
    {
        var message = await FixtureMessage();

        var (app, url) = await StartStub(context =>
        {
            context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return Task.CompletedTask;
        });

        try
        {
            var result = await Transmitter(url, timeoutSeconds: 30).Transmit(message);

            Assert.Equal(CdcTransmissionStatus.Rejected, result.Status);
            Assert.Contains("422", result.Detail);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task AnUnresponsiveStubRecordsTimedOut()
    {
        var message = await FixtureMessage();

        var (app, url) = await StartStub(async context =>
        {
            // Hold the request past the transmitter's timeout, releasing when
            // the client aborts so the stub does not outlive the test.
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), context.RequestAborted);
            }
            catch (OperationCanceledException)
            {
            }
        });

        try
        {
            var result = await Transmitter(url, timeoutSeconds: 1).Transmit(message);

            Assert.Equal(CdcTransmissionStatus.TimedOut, result.Status);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }
}
