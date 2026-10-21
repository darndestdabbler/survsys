using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The export queue worker's pass against the real database and a stub CDC
/// endpoint: a queued case through to its log row, a refused send logged as a
/// failure, and a held case left alone.
/// </summary>
/// <remarks>
/// The queue is filled the way it is filled, by opening a case, which the
/// auto-queue puts in the queue unheld. Every case is cleared before each test
/// as the other case tests clear them, so the pass sees only the case opened
/// here; the cascade takes its queue entry and log rows with it.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class NotificationQueueServiceTests(SqlServerFixture server) : IDisposable
{
    private static readonly NotificationQueueOptions Options = new()
    {
        ProcessingId = NotificationProcessing.Training,
        Queue = NotificationQueueType.Onboarding,
    };

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    /// <summary>
    /// An accepting endpoint: the case leaves the queue and its log row is
    /// accepted, sent, carries the message the endpoint received, the
    /// processing id and queue, and the build's warning.
    /// </summary>
    [Fact]
    public async Task AQueuedCaseIsSentAndLogged()
    {
        await using var context = server.NewContext();
        await Clear(context);
        string? received = null;
        var (app, url) = await StartStub(async request =>
        {
            received = await new StreamReader(request.Request.Body).ReadToEndAsync();
            request.Response.StatusCode = StatusCodes.Status200OK;
        });

        try
        {
            var report = await OpenQueued();
            await NotificationReference.Seed(context, report.ConditionId);

            var processed = await Process(context, url);

            Assert.Equal(1, processed);
            Assert.False(await context.NotificationQueue.AnyAsync(entry => entry.PersonConditionId == report.Id));
            var log = await context.CdcExportLog.Include(entry => entry.Details)
                .SingleAsync(entry => entry.PersonConditionId == report.Id);
            Assert.Equal(NotificationQueueService.AcceptedOutcome, log.Outcome);
            Assert.NotNull(log.SentAt);
            Assert.Equal(report.RecordNumber, log.RecordNumber);
            Assert.Equal(received, log.GeneratedMessage);
            Assert.Equal("T", log.ProcessingId);
            Assert.Equal("onboarding", log.Queue);
            var detail = Assert.Single(log.Details);
            Assert.Equal(NotificationReference.UnformattedVariable, detail.PhinVar);
            Assert.Equal("Warning", detail.Severity);
        }
        finally
        {
            await app.DisposeAsync();
            await NotificationReference.Clear(context);
            await Clear(context);
        }
    }

    /// <summary>
    /// A refusing endpoint: the case still leaves the queue, and its log row is
    /// a failure with the refusal in its detail and no sent time.
    /// </summary>
    [Fact]
    public async Task ARefusedSendIsLoggedAsAFailure()
    {
        await using var context = server.NewContext();
        await Clear(context);
        var (app, url) = await StartStub(request =>
        {
            request.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return Task.CompletedTask;
        });

        try
        {
            var report = await OpenQueued();
            await NotificationReference.Seed(context, report.ConditionId);

            Assert.Equal(1, await Process(context, url));

            Assert.False(await context.NotificationQueue.AnyAsync(entry => entry.PersonConditionId == report.Id));
            var log = await context.CdcExportLog.SingleAsync(entry => entry.PersonConditionId == report.Id);
            Assert.Equal(NotificationQueueService.FailedOutcome, log.Outcome);
            Assert.Null(log.SentAt);
            Assert.Contains("422", log.Detail, StringComparison.Ordinal);
            Assert.NotNull(log.GeneratedMessage);
        }
        finally
        {
            await app.DisposeAsync();
            await NotificationReference.Clear(context);
            await Clear(context);
        }
    }

    /// <summary>
    /// Two passes run side by side over one queued case: between them it is
    /// processed once, sent once and logged once.
    /// </summary>
    [Fact]
    public async Task TwoPassesSideBySideSendACaseOnce()
    {
        await using var context = server.NewContext();
        await Clear(context);
        var sends = 0;
        var (app, url) = await StartStub(request =>
        {
            Interlocked.Increment(ref sends);
            request.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        try
        {
            var report = await OpenQueued();
            await NotificationReference.Seed(context, report.ConditionId);

            await using var first = server.NewContext();
            await using var second = server.NewContext();
            var processed = await Task.WhenAll(Process(first, url), Process(second, url));

            Assert.Equal(1, processed.Sum());
            Assert.Equal(1, sends);
            Assert.Equal(1, await context.CdcExportLog.CountAsync(entry => entry.PersonConditionId == report.Id));
        }
        finally
        {
            await app.DisposeAsync();
            await NotificationReference.Clear(context);
            await Clear(context);
        }
    }

    /// <summary>
    /// A pass cancelled before it sends leaves the case queued and unlogged;
    /// one cancelled while its send is in flight sees the send through and
    /// logs it, so the case is sent once and never queued again.
    /// </summary>
    [Fact]
    public async Task ACancelledPassNeitherLosesNorResendsTheCase()
    {
        await using var context = server.NewContext();
        await Clear(context);
        var sends = 0;
        var sending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (app, url) = await StartStub(async request =>
        {
            Interlocked.Increment(ref sends);
            sending.TrySetResult();
            await release.Task;
            request.Response.StatusCode = StatusCodes.Status200OK;
        });

        try
        {
            var report = await OpenQueued();
            await NotificationReference.Seed(context, report.ConditionId);

            await using (var cancelledContext = server.NewContext())
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => Process(cancelledContext, url, new CancellationToken(canceled: true)));
            }

            Assert.True(await context.NotificationQueue.AnyAsync(entry => entry.PersonConditionId == report.Id));
            Assert.False(await context.CdcExportLog.AnyAsync(entry => entry.PersonConditionId == report.Id));

            await using var passContext = server.NewContext();
            using var cancellation = new CancellationTokenSource();
            var pass = Process(passContext, url, cancellation.Token);
            await sending.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await cancellation.CancelAsync();
            release.SetResult();

            Assert.Equal(1, await pass.WaitAsync(TimeSpan.FromSeconds(30)));
            Assert.Equal(1, sends);
            Assert.False(await context.NotificationQueue.AnyAsync(entry => entry.PersonConditionId == report.Id));
            var log = await context.CdcExportLog.SingleAsync(entry => entry.PersonConditionId == report.Id);
            Assert.Equal(NotificationQueueService.AcceptedOutcome, log.Outcome);
        }
        finally
        {
            await app.DisposeAsync();
            await NotificationReference.Clear(context);
            await Clear(context);
        }
    }

    /// <summary>A held case is neither sent nor logged, and stays on the queue.</summary>
    [Fact]
    public async Task AHeldCaseIsLeftAlone()
    {
        await using var context = server.NewContext();
        await Clear(context);
        var (app, url) = await StartStub(request =>
        {
            request.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        try
        {
            var report = await OpenQueued();
            await context.NotificationQueue
                .Where(entry => entry.PersonConditionId == report.Id)
                .ExecuteUpdateAsync(entry => entry.SetProperty(item => item.Hold, true));

            Assert.Equal(0, await Process(context, url));

            Assert.True(await context.NotificationQueue.AnyAsync(entry => entry.PersonConditionId == report.Id));
            Assert.False(await context.CdcExportLog.AnyAsync(entry => entry.PersonConditionId == report.Id));
        }
        finally
        {
            await app.DisposeAsync();
            await Clear(context);
        }
    }

    private static Task<int> Process(
        SurvsysDbContext context, string url, CancellationToken cancellationToken = default) =>
        NotificationQueueService.Process(
            context,
            new NotificationMessageBuilder(context),
            new CdcTransmitter(
                new HttpClient(),
                Microsoft.Extensions.Options.Options.Create(new CdcTransmissionOptions { Endpoint = url }),
                NullLogger<CdcTransmitter>.Instance),
            Options,
            TimeProvider.System,
            cancellationToken);

    /// <summary>
    /// A loopback host that answers every request with <paramref name="handler"/>,
    /// bound to an OS-assigned port, as <see cref="CdcTransmitterTests"/> starts one.
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

    /// <summary>Takes every case out, which cascades to the queue and the export log.</summary>
    private static Task Clear(SurvsysDbContext context) => context.PersonConditions.ExecuteDeleteAsync();

    /// <summary>
    /// Opens a case as <see cref="NotificationMessageBuilderTests.Open"/> does and
    /// confirms it, which its condition's seeded print criteria notify, so the
    /// change queues it unheld.
    /// </summary>
    private async Task<PersonConditionResponse> OpenQueued()
    {
        var report = await NotificationMessageBuilderTests.Open(api);

        using var status = await api.CreateClient().PutAsJsonAsync(
            $"/api/person-conditions/{report.Id}/status",
            new UpdateCaseStatusRequest(CaseStatusRules.ConfirmedCode),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);

        return report;
    }
}
