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
/// assigned message is refused and changes nothing. Each leaves its states on
/// the message's audit trail.
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

    /// <summary>
    /// Each state a hand move, a retry and its translation put a message in is
    /// on its trail, which the detail returns; the retry is counted; and a
    /// message held for a reason a person resolves is refused a retry.
    /// </summary>
    [Fact]
    public async Task ActionsLeaveTheTrailAndARetryOfAPersonsExceptionIsRefused()
    {
        var client = api.CreateClient();
        var moving = await Stage(MessageReviewStatusRules.EntryCode);
        var held = await Stage(MessageReviewStatusRules.ExceptionCode, sender: "NO_SUCH_LAB");
        var personal = await Stage(
            MessageReviewStatusRules.ExceptionCode, category: MessageExceptionRules.WhitelistRuleExceptionCode);

        await Act(client, "move", new StagedMessageActionRequest([moving], "semi-auto"));
        await Act(client, "move", new StagedMessageActionRequest([moving], "semi-auto"));
        await Act(client, "retry", new StagedMessageActionRequest([held]));

        using (var refused = await client.PostAsJsonAsync(
            "/api/staged-messages/retry", new StagedMessageActionRequest([personal]), Json))
        {
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        }

        // A second move into the state the message is already in adds nothing.
        Assert.Equal(
            [MessageReviewStatusRules.EntryCode, MessageReviewStatusRules.SemiAutoCode],
            (await History(client, moving)).Select(state => state.ReviewStatus));

        var trail = await History(client, held);
        Assert.Equal(
            [MessageReviewStatusRules.ExceptionCode, MessageReviewStatusRules.EntryCode, MessageReviewStatusRules.ExceptionCode],
            trail.Select(state => state.ReviewStatus));
        Assert.Equal(MessageExceptionRules.UnknownSenderCode, trail[2].ExceptionCategory);

        await using var context = server.NewContext();
        var retried = await context.StagedMessages.SingleAsync(message => message.Id == held);
        Assert.Equal(1, retried.RetryCount);
        Assert.Equal(MessageExceptionRules.UnknownSenderCode, retried.ExceptionCategory);
        Assert.Equal(
            MessageReviewStatusRules.ExceptionCode,
            (await context.StagedMessages.SingleAsync(message => message.Id == personal)).ReviewStatus);
    }

    /// <summary>
    /// A message moved by hand to pending keeps why it is held; moved on to a
    /// state that holds it for nothing, it keeps no exception detail or
    /// category, and its trail says so.
    /// </summary>
    [Fact]
    public async Task AMoveKeepsWhyAMessageIsHeldOnlyWhileItIsHeld()
    {
        var client = api.CreateClient();
        var held = await Stage(
            MessageReviewStatusRules.ExceptionCode, category: MessageExceptionRules.WhitelistRuleExceptionCode);

        await Act(client, "move", new StagedMessageActionRequest([held], "pending"));
        await using (var context = server.NewContext())
        {
            var pending = await context.StagedMessages.SingleAsync(message => message.Id == held);
            Assert.Equal(MessageExceptionRules.WhitelistRuleExceptionCode, pending.ExceptionCategory);
            Assert.Equal("Held for the queue tests.", pending.ExceptionDetail);
        }

        await Act(client, "move", new StagedMessageActionRequest([held], "semi-auto"));
        await using (var context = server.NewContext())
        {
            var moved = await context.StagedMessages.SingleAsync(message => message.Id == held);
            Assert.Null(moved.ExceptionCategory);
            Assert.Null(moved.ExceptionDetail);
        }

        var trail = await History(client, held);
        Assert.Equal(
            [MessageReviewStatusRules.ExceptionCode, MessageReviewStatusRules.PendingCode, MessageReviewStatusRules.SemiAutoCode],
            trail.Select(state => state.ReviewStatus));
        Assert.Equal(MessageExceptionRules.WhitelistRuleExceptionCode, trail[1].ExceptionCategory);
        Assert.Null(trail[2].ExceptionCategory);
    }

    private static async Task<IReadOnlyList<StagedMessageAuditResponse>> History(HttpClient client, int id)
    {
        var detail = await client.GetFromJsonAsync<StagedMessageDetailResponse>($"/api/staged-messages/{id}", Json);

        return detail!.History!;
    }

    private static async Task<StagedMessageActionResponse> Act(
        HttpClient client, string action, StagedMessageActionRequest request)
    {
        using var response = await client.PostAsJsonAsync($"/api/staged-messages/{action}", request, Json);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<StagedMessageActionResponse>(Json))!;
    }

    /// <summary>A parsed message with one result, in the state given.</summary>
    private async Task<int> Stage(string status, string sender = "QUEUE_LAB", string? category = null)
    {
        await using var context = server.NewContext();

        var message = new StagedMessage
        {
            RawMessage = "MSH|^~\\&|",
            Source = "queue-actions.hl7",
            ReceivedAt = DateTime.UtcNow,
            ReviewStatus = status,
            ExceptionCategory = category,
            ExceptionDetail = category is null ? null : "Held for the queue tests.",
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
