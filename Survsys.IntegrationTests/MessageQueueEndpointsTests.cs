using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The queue's move, delete and retry against the real host and database:
/// each acts on every message named, a delete takes the message's results with
/// it, a retry translates a held message again, and an action naming an
/// assigned message is refused and changes nothing.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class MessageQueueEndpointsTests(SqlServerFixture server) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public Task InitializeAsync() => Clear();

    public async Task DisposeAsync()
    {
        await Clear();
        api.Dispose();
    }

    [Fact]
    public async Task MoveDeleteAndRetryActOnEveryMessageNamed()
    {
        var client = api.CreateClient();
        var first = await Stage(MessageReviewStatusRules.EntryCode);
        var second = await Stage(MessageReviewStatusRules.PendingCode);
        var held = await Stage(MessageReviewStatusRules.ExceptionCode, sender: "NO_SUCH_LAB");

        var moved = await Act(client, "move", new StagedMessageActionRequest([first, second], "semi-auto"));
        Assert.Equal(2, moved.Count);
        Assert.Equal(MessageReviewStatusRules.SemiAutoCode, await StatusOf(first));
        Assert.Equal(MessageReviewStatusRules.SemiAutoCode, await StatusOf(second));

        // The sender is still not on file, so the retry translates the message
        // again and holds it again, with why.
        var retried = await Act(client, "retry", new StagedMessageActionRequest([held]));
        Assert.Equal(1, retried.Count);
        await using (var context = server.NewContext())
        {
            var message = await context.StagedMessages.SingleAsync(staged => staged.Id == held);
            Assert.Equal(MessageReviewStatusRules.ExceptionCode, message.ReviewStatus);
            Assert.Contains("NO_SUCH_LAB", message.ExceptionDetail, StringComparison.Ordinal);
        }

        var deleted = await Act(client, "delete", new StagedMessageActionRequest([first, held]));
        Assert.Equal(2, deleted.Count);
        await using (var context = server.NewContext())
        {
            Assert.False(await context.StagedMessages.AnyAsync(staged => staged.Id == first || staged.Id == held));
            Assert.False(await context.Set<StagedMessageResult>().AnyAsync(
                result => result.StagedMessageId == first || result.StagedMessageId == held));
            Assert.True(await context.StagedMessages.AnyAsync(staged => staged.Id == second));
        }
    }

    [Fact]
    public async Task AnActionNamingAnAssignedMessageIsRefusedAndChangesNothing()
    {
        var client = api.CreateClient();
        var waiting = await Stage(MessageReviewStatusRules.EntryCode);
        var assigned = await Stage(MessageReviewStatusRules.AssignedCode);

        foreach (var (action, request) in new[]
        {
            ("move", new StagedMessageActionRequest([waiting, assigned], "pending")),
            ("delete", new StagedMessageActionRequest([waiting, assigned])),
        })
        {
            using var response = await client.PostAsJsonAsync($"/api/staged-messages/{action}", request, Json);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        Assert.Equal(MessageReviewStatusRules.EntryCode, await StatusOf(waiting));
        Assert.Equal(MessageReviewStatusRules.AssignedCode, await StatusOf(assigned));

        using var missing = await client.PostAsJsonAsync(
            "/api/staged-messages/delete", new StagedMessageActionRequest([int.MaxValue]), Json);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private static async Task<StagedMessageActionResponse> Act(
        HttpClient client, string action, StagedMessageActionRequest request)
    {
        using var response = await client.PostAsJsonAsync($"/api/staged-messages/{action}", request, Json);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<StagedMessageActionResponse>(Json))!;
    }

    /// <summary>A parsed message with one result, in the state given.</summary>
    private async Task<int> Stage(string status, string sender = "QUEUE_LAB")
    {
        await using var context = server.NewContext();

        var message = new StagedMessage
        {
            RawMessage = "MSH|^~\\&|",
            Source = "queue-actions.hl7",
            ReceivedAt = DateTime.UtcNow,
            ReviewStatus = status,
            MessageType = "ORU^R01",
            SendingFacility = sender,
            Results = [new StagedMessageResult { Sequence = 0, ObservationCode = "11585-7" }],
        };
        context.StagedMessages.Add(message);
        await context.SaveChangesAsync();

        return message.Id;
    }

    private async Task<string> StatusOf(int id)
    {
        await using var context = server.NewContext();

        return await context.StagedMessages
            .Where(message => message.Id == id)
            .Select(message => message.ReviewStatus)
            .SingleAsync();
    }

    private async Task Clear()
    {
        await using var context = server.NewContext();

        await context.StagedMessages.ExecuteDeleteAsync();
    }
}
