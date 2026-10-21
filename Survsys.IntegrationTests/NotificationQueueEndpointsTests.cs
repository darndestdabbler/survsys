using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The notification-queue endpoints, against the real database: that a queued
/// case is read back oldest first with its record number, patient and
/// condition joined, and that its hold is raised and then lowered through the
/// hold endpoint.
/// </summary>
/// <remarks>
/// Driven through the endpoints, with the queue filled the one way it is
/// filled — by opening a case, which the auto-queue puts in the queue unheld.
/// The person-condition table is cleared before and after each test as the
/// other case tests clear it; the cascade from the queue to
/// <c>person_conditions</c> takes the queue entries with it, so nothing here
/// clears the queue of its own.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class NotificationQueueEndpointsTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    /// <summary>
    /// A paged read returns the queued case with its case joined: the record
    /// number it was opened under, the patient last-name-first, the condition
    /// by name, and not held.
    /// </summary>
    [Fact]
    public async Task APagedReadReturnsTheQueuedCaseWithItsCaseJoined()
    {
        await Clear();

        try
        {
            var report = await Open();

            using var response = await api.CreateClient().GetAsync("/api/notification-queue?pageSize=200");
            response.EnsureSuccessStatusCode();

            var page = (await response.Content
                .ReadFromJsonAsync<PagedResult<NotificationQueueItemResponse>>(Json))!;

            var row = Assert.Single(page.Items, item => item.PersonConditionId == report.Id);
            Assert.Equal(report.RecordNumber, row.RecordNumber);
            Assert.Equal("Rodriguez, Maria", row.PatientName);
            Assert.Equal("Malaria", row.Condition);
            Assert.False(row.Hold);
        }
        finally
        {
            await Clear();
        }
    }

    /// <summary>
    /// Processing an empty queue now answers that it processed nothing, and
    /// writes nothing to the export log.
    /// </summary>
    [Fact]
    public async Task ProcessingAnEmptyQueueProcessesNothing()
    {
        await Clear();

        using var response = await api.CreateClient().PostAsync("/api/notification-queue/process", content: null);
        response.EnsureSuccessStatusCode();

        var done = (await response.Content.ReadFromJsonAsync<NotificationQueueProcessResponse>(Json))!;
        Assert.Equal(0, done.Processed);

        await using var context = server.NewContext();
        Assert.False(await context.CdcExportLog.AnyAsync());
    }

    /// <summary>
    /// A hold is raised and then lowered: each PATCH answers with the entry in
    /// its new state, and a case that is not in the queue is a 404.
    /// </summary>
    [Fact]
    public async Task AHoldIsRaisedThenLowered()
    {
        await Clear();

        try
        {
            var report = await Open();

            using var raise = await api.CreateClient().PatchAsJsonAsync(
                $"/api/notification-queue/{report.Id}/hold",
                new SetNotificationHoldRequest(true),
                Json);
            Assert.Equal(HttpStatusCode.OK, raise.StatusCode);
            var raised = (await raise.Content
                .ReadFromJsonAsync<NotificationQueueItemResponse>(Json))!;
            Assert.True(raised.Hold);

            using var lower = await api.CreateClient().PatchAsJsonAsync(
                $"/api/notification-queue/{report.Id}/hold",
                new SetNotificationHoldRequest(false),
                Json);
            Assert.Equal(HttpStatusCode.OK, lower.StatusCode);
            var lowered = (await lower.Content
                .ReadFromJsonAsync<NotificationQueueItemResponse>(Json))!;
            Assert.False(lowered.Hold);

            using var missing = await api.CreateClient().PatchAsJsonAsync(
                "/api/notification-queue/987654321/hold",
                new SetNotificationHoldRequest(true),
                Json);
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }
        finally
        {
            await Clear();
        }
    }

    /// <summary>
    /// Queueing a case by hand re-creates its queue entry through the same path
    /// the auto-queue takes: the entry removed is written again and the row
    /// comes back unheld, and a case that is not there is a 404.
    /// </summary>
    [Fact]
    public async Task QueueingACaseByHandPutsItBackInTheQueue()
    {
        await Clear();

        try
        {
            var report = await Open();

            // Confirming auto-queues the case; take its entry out so the manual
            // queue has something to re-create rather than only to refresh.
            await using (var context = server.NewContext())
            {
                await context.NotificationQueue
                    .Where(entry => entry.PersonConditionId == report.Id)
                    .ExecuteDeleteAsync();
            }

            using var response = await api.CreateClient().PostAsync(
                $"/api/person-conditions/{report.Id}/notification-queue",
                content: null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var row = (await response.Content
                .ReadFromJsonAsync<NotificationQueueItemResponse>(Json))!;
            Assert.Equal(report.Id, row.PersonConditionId);
            Assert.False(row.Hold);

            await using (var context = server.NewContext())
            {
                Assert.True(await context.NotificationQueue
                    .AnyAsync(entry => entry.PersonConditionId == report.Id));
            }

            using var missing = await api.CreateClient().PostAsync(
                "/api/person-conditions/987654321/notification-queue",
                content: null);
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }
        finally
        {
            await Clear();
        }
    }

    /// <summary>
    /// Removing a queued case takes its entry off the queue, and removing it
    /// again is a 404.
    /// </summary>
    [Fact]
    public async Task ARemovedCaseLeavesTheQueue()
    {
        await Clear();

        try
        {
            var report = await Open();
            var client = api.CreateClient();

            using var removed = await client.DeleteAsync($"/api/notification-queue/{report.Id}");
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
            Assert.False(await IsQueued(report.Id));

            using var again = await client.DeleteAsync($"/api/notification-queue/{report.Id}");
            Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        }
        finally
        {
            await Clear();
        }
    }

    /// <summary>
    /// A case CDC never accepted cannot be retracted; once an accepted export
    /// is logged, a held entry is queued again as a released retraction.
    /// </summary>
    [Fact]
    public async Task ARetractionNeedsAnAcceptedNotificationAndIsQueuedReleased()
    {
        await Clear();

        try
        {
            var report = await Open();
            var client = api.CreateClient();
            using var hold = await client.PatchAsJsonAsync(
                $"/api/notification-queue/{report.Id}/hold", new SetNotificationHoldRequest(true), Json);
            hold.EnsureSuccessStatusCode();

            using var refused = await client.PostAsync($"/api/notification-queue/{report.Id}/retract", null);
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

            await using (var context = server.NewContext())
            {
                context.CdcExportLog.Add(new CdcExportLogEntry
                {
                    PersonConditionId = report.Id,
                    RecordNumber = report.RecordNumber,
                    QueuedAt = DateTime.UtcNow,
                    SentAt = DateTime.UtcNow,
                    Outcome = NotificationQueueService.AcceptedOutcome,
                });
                await context.SaveChangesAsync();
            }

            using var retracted = await client.PostAsync($"/api/notification-queue/{report.Id}/retract", null);
            Assert.Equal(HttpStatusCode.OK, retracted.StatusCode);
            var row = (await retracted.Content.ReadFromJsonAsync<NotificationQueueItemResponse>(Json))!;
            Assert.True(row.Retract);
            Assert.False(row.Hold);
        }
        finally
        {
            await Clear();
        }
    }

    /// <summary>
    /// Exporting by record number queues each case on file, whatever its status,
    /// and names the numbers no case has.
    /// </summary>
    [Fact]
    public async Task ExportingRecordsQueuesTheKnownCasesAndNamesTheRest()
    {
        await Clear();

        try
        {
            var report = await Open();
            await Unqueue(report.Id);

            using var response = await api.CreateClient().PostAsJsonAsync(
                "/api/notification-queue/records",
                new ExportRecordsRequest([report.RecordNumber, " ", "NOPE-1", report.RecordNumber]),
                Json);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var done = (await response.Content.ReadFromJsonAsync<NotificationExportResponse>(Json))!;
            Assert.Equal(1, done.Queued);
            Assert.Equal(["NOPE-1"], done.Unknown);
            Assert.True(await IsQueued(report.Id));
        }
        finally
        {
            await Clear();
        }
    }

    /// <summary>
    /// Exporting an MMWR range queues the cases of the range whose status their
    /// print criteria hold — the confirmed one, not the suspected one or the one
    /// outside the range — and a range that runs backwards is refused.
    /// </summary>
    [Fact]
    public async Task ExportingAnMmwrRangeQueuesItsNotifiableCases()
    {
        await Clear();

        try
        {
            var inRange = await Open();
            var suspected = await Open();
            var outside = await Open();
            await using (var context = server.NewContext())
            {
                foreach (var (id, week, status) in new[]
                {
                    (inRange.Id, 10, CaseStatusRules.ConfirmedCode),
                    (suspected.Id, 11, CaseStatusRules.SuspectedCode),
                    (outside.Id, 20, CaseStatusRules.ConfirmedCode),
                })
                {
                    await context.PersonConditions.Where(report => report.Id == id).ExecuteUpdateAsync(set => set
                        .SetProperty(report => report.MmwrYear, 2026)
                        .SetProperty(report => report.MmwrWeek, week)
                        .SetProperty(report => report.CaseStatus, status));
                }

                await context.NotificationQueue.ExecuteDeleteAsync();
            }

            var client = api.CreateClient();
            using var response = await client.PostAsJsonAsync(
                "/api/notification-queue/mmwr-range", new ExportMmwrRangeRequest(2026, 9, 12, inRange.ConditionId), Json);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Assert.Equal(1, (await response.Content.ReadFromJsonAsync<NotificationExportResponse>(Json))!.Queued);
            Assert.True(await IsQueued(inRange.Id));
            Assert.False(await IsQueued(suspected.Id));
            Assert.False(await IsQueued(outside.Id));

            using var backwards = await client.PostAsJsonAsync(
                "/api/notification-queue/mmwr-range", new ExportMmwrRangeRequest(2026, 12, 9), Json);
            Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
        }
        finally
        {
            await Clear();
        }
    }

    private async Task<bool> IsQueued(int reportId)
    {
        await using var context = server.NewContext();
        return await context.NotificationQueue.AnyAsync(entry => entry.PersonConditionId == reportId);
    }

    private async Task Unqueue(int reportId)
    {
        await using var context = server.NewContext();
        await context.NotificationQueue.Where(entry => entry.PersonConditionId == reportId).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Opens a case about a seeded person for a seeded active condition, and
    /// answers with it, confirmed: the change queues it for notification unheld,
    /// its condition's seeded print criteria notifying a confirmed case.
    /// </summary>
    private async Task<PersonConditionResponse> Open()
    {
        var person = await PersonId("Rodriguez, Maria");
        var condition = await ConditionId("Malaria");

        using var response = await api.CreateClient().PostAsJsonAsync(
            "/api/person-conditions",
            new CreatePersonConditionRequest(person, condition),
            Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var report = (await response.Content.ReadFromJsonAsync<PersonConditionResponse>(Json))!;

        using var status = await api.CreateClient().PutAsJsonAsync(
            $"/api/person-conditions/{report.Id}/status",
            new UpdateCaseStatusRequest(CaseStatusRules.ConfirmedCode),
            Json);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);

        return report;
    }

    private async Task<int> PersonId(string named)
    {
        using var response = await api.CreateClient().GetAsync("/api/persons?pageSize=200");
        response.EnsureSuccessStatusCode();

        var page = (await response.Content
            .ReadFromJsonAsync<PagedResult<PersonResponse>>(Json))!;

        return page.Items
            .Single(person => $"{person.LastName}, {person.FirstName}" == named)
            .Id;
    }

    private async Task<int> ConditionId(string named)
    {
        using var response = await api.CreateClient().GetAsync("/api/conditions?pageSize=200");
        response.EnsureSuccessStatusCode();

        var page = (await response.Content
            .ReadFromJsonAsync<PagedResult<ConditionResponse>>(Json))!;

        return page.Items.Single(condition => condition.Name == named).Id;
    }

    /// <summary>
    /// Takes every case out of the table, which cascades to the queue, so each
    /// test starts and ends with an empty queue.
    /// </summary>
    private async Task Clear()
    {
        await using var context = server.NewContext();

        await context.PersonConditions.ExecuteDeleteAsync();
    }
}
