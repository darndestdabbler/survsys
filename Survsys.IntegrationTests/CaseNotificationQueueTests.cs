using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The auto-queue, against the real database: that changing a case whose
/// status meets its condition's print criteria leaves it queued for CDC
/// notification, as the legacy database trigger did, but written here in
/// application code off a domain event rather than by a trigger.
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
    /// A case is queued only once its status meets its condition's print
    /// criteria: opened unclassified it is not queued, set to Suspected — not
    /// among the seeded Confirmed and Probable — it still is not, and set to
    /// Confirmed it is queued exactly once, unheld.
    /// </summary>
    [Fact]
    public async Task ACaseIsQueuedOnlyWhenItsStatusMeetsItsPrintCriteria()
    {
        await Clear();

        try
        {
            var report = await Open();
            Assert.Empty(await QueueFor(report.Id));

            await SetStatus(report.Id, CaseStatusRules.SuspectedCode);
            Assert.Empty(await QueueFor(report.Id));

            await SetStatus(report.Id, CaseStatusRules.ConfirmedCode);

            var entry = Assert.Single(await QueueFor(report.Id));
            Assert.Equal(report.Id, entry.PersonConditionId);
            Assert.False(entry.Hold);
        }
        finally
        {
            await Clear();
        }
    }

    /// <summary>
    /// Changing a queued case again refreshes its one entry rather than adding
    /// a second: the queue stays one-to-a-case, and the queued time moves to
    /// the later change.
    /// </summary>
    [Fact]
    public async Task ChangingACaseRefreshesItsOneEntryRatherThanDuplicating()
    {
        await Clear();

        try
        {
            var report = await Open();
            await SetStatus(report.Id, CaseStatusRules.ConfirmedCode);
            var first = Assert.Single(await QueueFor(report.Id)).QueuedAt;

            await SetStatus(report.Id, CaseStatusRules.ProbableCode);

            var entry = Assert.Single(await QueueFor(report.Id));
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
    /// A workflow move queues a case its print criteria notify, and leaves an
    /// unclassified case — which no criterion holds — off the queue.
    /// </summary>
    [Fact]
    public async Task AWorkflowMoveQueuesOnlyANotifiableCase()
    {
        await Clear();

        try
        {
            var notifiable = await Open();
            await SetStatus(notifiable.Id, CaseStatusRules.ConfirmedCode);
            var unclassified = await Open();
            await using (var context = server.NewContext())
            {
                await context.NotificationQueue.ExecuteDeleteAsync();
            }

            foreach (var id in new[] { notifiable.Id, unclassified.Id })
            {
                using var moved = await api.CreateClient().PutAsJsonAsync(
                    $"/api/person-conditions/{id}/workflow", new WorkflowTransitionRequest(WorkflowRules.AcceptCode), Json);
                Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
            }

            Assert.Single(await QueueFor(notifiable.Id));
            Assert.Empty(await QueueFor(unclassified.Id));
        }
        finally
        {
            await Clear();
        }
    }

    private async Task SetStatus(int reportId, string code)
    {
        using var status = await api.CreateClient().PutAsJsonAsync(
            $"/api/person-conditions/{reportId}/status", new UpdateCaseStatusRequest(code), Json);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
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
