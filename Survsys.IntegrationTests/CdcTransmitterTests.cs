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
/// needs neither <c>Survsys.Messaging</c> nor the database. The file transport
/// writes only into a temporary test folder.
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
    public async Task AConfiguredUserNameSignsThePostInWithBasicCredentials()
    {
        var message = await FixtureMessage();
        string? authorization = null;

        var (app, url) = await StartStub(context =>
        {
            authorization = context.Request.Headers.Authorization;
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        try
        {
            var transmitter = new CdcTransmitter(
                new HttpClient(),
                Options.Create(new CdcTransmissionOptions
                {
                    Endpoint = url,
                    UserName = "survsys",
                    Password = "stub-password",
                }),
                NullLogger<CdcTransmitter>.Instance);

            var result = await transmitter.Transmit(message);

            Assert.Equal(CdcTransmissionStatus.Accepted, result.Status);
            Assert.Equal(
                "Basic " + Convert.ToBase64String("survsys:stub-password"u8.ToArray()),
                authorization);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task WithoutAUserNameThePostCarriesNoCredentials()
    {
        var message = await FixtureMessage();
        var authorized = true;

        var (app, url) = await StartStub(context =>
        {
            authorized = context.Request.Headers.ContainsKey("Authorization");
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        try
        {
            await Transmitter(url, timeoutSeconds: 30).Transmit(message);

            Assert.False(authorized);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    /// <summary>
    /// The file transport into a test folder: one .hl7 file holding the message
    /// unchanged, named by the result's batch file, and nothing left partial.
    /// </summary>
    [Fact]
    public async Task TheFileTransportWritesTheMessageIntoTheOutbox()
    {
        var message = await FixtureMessage();
        var outbox = Directory.CreateTempSubdirectory("survsys-outbox-").FullName;

        try
        {
            var transmitter = new CdcTransmitter(
                new HttpClient(),
                Options.Create(new CdcTransmissionOptions { Transport = CdcTransport.File, OutboxDirectory = outbox }),
                NullLogger<CdcTransmitter>.Instance);

            var result = await transmitter.Transmit(message);

            Assert.Equal(CdcTransmissionStatus.Accepted, result.Status);
            var written = Assert.Single(Directory.GetFiles(outbox));
            Assert.Equal(result.BatchFile, Path.GetFileName(written));
            Assert.EndsWith(".hl7", written, StringComparison.Ordinal);
            Assert.Equal(message, await File.ReadAllTextAsync(written));
        }
        finally
        {
            Directory.Delete(outbox, recursive: true);
        }
    }

    [Fact]
    public async Task AnOutboxThatIsNotThereRecordsFaulted()
    {
        var outbox = Path.Combine(Path.GetTempPath(), $"survsys-missing-{Guid.NewGuid():N}");
        var transmitter = new CdcTransmitter(
            new HttpClient(),
            Options.Create(new CdcTransmissionOptions { Transport = CdcTransport.File, OutboxDirectory = outbox }),
            NullLogger<CdcTransmitter>.Instance);

        var result = await transmitter.Transmit(await FixtureMessage());

        Assert.Equal(CdcTransmissionStatus.Faulted, result.Status);
        Assert.Null(result.BatchFile);
        Assert.False(Directory.Exists(outbox));
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
