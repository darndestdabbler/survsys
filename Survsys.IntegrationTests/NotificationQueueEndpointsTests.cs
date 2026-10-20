using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
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

            // Opening auto-queues the case; take its entry out so the manual
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
    /// Opens a case about a seeded person for a seeded active condition, and
    /// answers with it. Opening it queues it for notification unheld.
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

        return (await response.Content.ReadFromJsonAsync<PersonConditionResponse>(Json))!;
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
