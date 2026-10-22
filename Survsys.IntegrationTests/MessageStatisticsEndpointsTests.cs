using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The intake statistics against the real host and database: messages counted
/// by sender, state and the day they were received, inside the range asked for.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class MessageStatisticsEndpointsTests(SqlServerFixture server) : IAsyncLifetime
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
    public async Task MessagesAreCountedBySenderStateAndDayInsideTheRange()
    {
        await using (var context = server.NewContext())
        {
            context.StagedMessages.AddRange(
                Message("ARUP", MessageReviewStatusRules.AssignedCode, new DateTime(2026, 3, 2, 8, 0, 0)),
                Message("ARUP", MessageReviewStatusRules.AssignedCode, new DateTime(2026, 3, 2, 23, 59, 0)),
                Message("ARUP", MessageReviewStatusRules.ExceptionCode, new DateTime(2026, 3, 2, 9, 0, 0)),
                Message(null, MessageReviewStatusRules.ExceptionCode, new DateTime(2026, 3, 3, 1, 0, 0)),
                Message("ARUP", MessageReviewStatusRules.AssignedCode, new DateTime(2026, 3, 4, 0, 0, 0)));
            await context.SaveChangesAsync();
        }

        var client = api.CreateClient();
        var rows = await client.GetFromJsonAsync<List<MessageStatisticsRowResponse>>(
            "/api/staged-messages/statistics?from=2026-03-02&to=2026-03-03", Json);

        Assert.Equal(
            [
                new MessageStatisticsRowResponse(null, MessageReviewStatusRules.ExceptionCode, new DateOnly(2026, 3, 3), 1),
                new MessageStatisticsRowResponse("ARUP", MessageReviewStatusRules.AssignedCode, new DateOnly(2026, 3, 2), 2),
                new MessageStatisticsRowResponse("ARUP", MessageReviewStatusRules.ExceptionCode, new DateOnly(2026, 3, 2), 1),
            ],
            rows);
    }

    [Fact]
    public async Task ARangeThatEndsBeforeItStartsIsRefused()
    {
        var client = api.CreateClient();

        using var response = await client.GetAsync("/api/staged-messages/statistics?from=2026-03-03&to=2026-03-02");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static StagedMessage Message(string? sender, string status, DateTime receivedAt) => new()
    {
        RawMessage = "MSH|^~\\&|",
        Source = "statistics.hl7",
        ReceivedAt = DateTime.SpecifyKind(receivedAt, DateTimeKind.Utc),
        ReviewStatus = status,
        SendingFacility = sender,
    };

    private async Task Clear()
    {
        await using var context = server.NewContext();

        await context.StagedMessages.ExecuteDeleteAsync();
    }
}
