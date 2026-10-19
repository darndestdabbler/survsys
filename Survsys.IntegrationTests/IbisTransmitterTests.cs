using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Survsys.Api;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// <see cref="IbisTransmitter"/> against a local stub endpoint rather than
/// IBIS, as <see cref="CdcTransmitterTests"/> proves the CDC transmitter: a
/// loopback host stands in for the receiving endpoint, so the POST, its JSON
/// body and the recorded outcome are proved end to end without a credential.
/// </summary>
public sealed class IbisTransmitterTests
{
    private static readonly IbisCaseRecord Record = new(
        "2026000017", "Chlamydia trachomatis infection", "Bear River", "C", new DateOnly(2026, 10, 1), 40, 2026);

    private static IbisTransmitter Transmitter(string? endpoint, int timeoutSeconds) =>
        new(
            new HttpClient(),
            Options.Create(new IbisTransmissionOptions { Endpoint = endpoint, TimeoutSeconds = timeoutSeconds }),
            NullLogger<IbisTransmitter>.Instance);

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
    public async Task AnAcceptingStubRecordsAcceptedAndReceivesTheRecordAsJson()
    {
        IbisCaseRecord? received = null;
        string? contentType = null;

        var (app, url) = await StartStub(async context =>
        {
            contentType = context.Request.ContentType;
            received = await context.Request.ReadFromJsonAsync<IbisCaseRecord>();
            context.Response.StatusCode = StatusCodes.Status200OK;
        });

        try
        {
            var result = await Transmitter(url, timeoutSeconds: 30).Transmit(Record);

            Assert.Equal(IbisTransmissionStatus.Accepted, result.Status);
            Assert.StartsWith("application/json", contentType);
            Assert.Equal(Record, received);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task ARejectingStubRecordsRejectedWithTheStatusInTheDetail()
    {
        var (app, url) = await StartStub(context =>
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return Task.CompletedTask;
        });

        try
        {
            var result = await Transmitter(url, timeoutSeconds: 30).Transmit(Record);

            Assert.Equal(IbisTransmissionStatus.Rejected, result.Status);
            Assert.Contains("503", result.Detail);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task AnUnresponsiveStubRecordsTimedOut()
    {
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
            var result = await Transmitter(url, timeoutSeconds: 1).Transmit(Record);

            Assert.Equal(IbisTransmissionStatus.TimedOut, result.Status);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task NoEndpointConfiguredRecordsFaultedWithoutARequest()
    {
        var result = await Transmitter(endpoint: null, timeoutSeconds: 30).Transmit(Record);

        Assert.Equal(IbisTransmissionStatus.Faulted, result.Status);
        Assert.Contains("no endpoint", result.Detail);
    }
}
