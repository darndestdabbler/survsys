using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The auto-queue, against the real database: that opening a case and then
/// changing it leaves the case queued for CDC notification, as the legacy
/// database trigger did, but written here in application code off a domain
/// event rather than by a trigger.
/// </summary>
/// <remarks>
/// Driven through the case endpoints, not the queue service directly, so what
/// is proved is the whole write path from the endpoint down to the row. The
/// person-condition table is cleared before and after each test as the other
/// case tests clear it; the cascade from <c>nmi_export_queue</c> to
/// <c>person_conditions</c> takes the queue entries with it, so nothing here
/// clears the queue of its own.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class CaseNotificationQueueTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    /// <summary>
    /// Opening a case queues it exactly once: one entry, naming the case, not
    /// held back from export.
    /// </summary>
    [Fact]
    public async Task ACreatedCaseIsQueuedExactlyOnceUnheld()
    {
        await Clear();

        try
        {
            var report = await Open();

            var queued = await QueueFor(report.Id);

            var entry = Assert.Single(queued);
            Assert.Equal(report.Id, entry.PersonConditionId);
            Assert.False(entry.Hold);
        }
        finally
        {
            await Clear();
        }
    }

    /// <summary>
    /// Changing a case again refreshes its one entry rather than adding a
    /// second: the queue stays one-to-a-case, and the queued time moves to the
    /// later change.
    /// </summary>
    [Fact]
    public async Task ChangingACaseRefreshesItsOneEntryRatherThanDuplicating()
    {
        await Clear();

        try
        {
            var report = await Open();
            var first = Assert.Single(await QueueFor(report.Id)).QueuedAt;

            using var status = await api.CreateClient().PutAsJsonAsync(
                $"/api/person-conditions/{report.Id}/status",
                new UpdateCaseStatusRequest(CaseStatusRules.ConfirmedCode),
                Json);
            Assert.Equal(HttpStatusCode.OK, status.StatusCode);

            var queued = await QueueFor(report.Id);

            var entry = Assert.Single(queued);
            Assert.True(
                entry.QueuedAt >= first,
                $"the refreshed queued time {entry.QueuedAt:O} is before the first {first:O}");
        }
        finally
        {
            await Clear();
        }
    }

    /// <summary>
    /// Opens a case about a seeded person for a seeded active condition, and
    /// answers with it.
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

    /// <summary>The queue entries a case has, read back through the context.</summary>
    private async Task<List<NotificationQueueEntry>> QueueFor(int reportId)
    {
        await using var context = server.NewContext();

        return await context.NotificationQueue
            .Where(entry => entry.PersonConditionId == reportId)
            .ToListAsync();
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
