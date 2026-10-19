using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The human-reviewed promotion of a staged message to a case, against the
/// real host and database: a message staged for review carried through to a
/// case morbidity report, the message left in the Assigned state, and a second
/// promotion of the same message refused because it is already a case.
/// </summary>
/// <remarks>
/// Where <see cref="ElrIntakeTests"/> carries a message from a dropped file to
/// queued-for-review, this carries one the rest of the way: it seeds a message
/// already in a promotable state — the state intake leaves a parseable message
/// in — and promotes it. The review states are the legacy EMSA queue states
/// <c>../nedss/docs/db-snapshots/session-013/</c> dumps on
/// <c>system_statuses</c>, Entry for a message waiting and Assigned (14) for
/// one promoted, as <see cref="SqlServerFixture"/>'s seeded record number
/// echoes session-011.
///
/// The person and condition the message is promoted to are the fixture's own
/// seeded rows, found over the wire by name so nothing here depends on an
/// identity value the fixture happened to assign. The report and staged tables
/// are cleared before and after, because the record number a promotion assigns
/// depends on the reports already there and the queue on the messages already
/// staged. The suite does not skip when SQL Server is absent;
/// <see cref="SqlServerFixture"/> fails it loudly instead.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class MessageQueuePromotionTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    /// <summary>
    /// The whole of what a promotion does and leaves behind: a case with the
    /// first record number of the year, the notification line naming it and
    /// its condition, the staged message moved to Assigned, and a second
    /// promotion of the same message refused rather than opening a second
    /// report.
    /// </summary>
    [Fact]
    public async Task APromotedMessageBecomesACaseAndCannotBePromotedAgain()
    {
        await Clear();

        var client = api.CreateClient();
        var year = DateTimeOffset.Now.Year;
        var staged = await StageMessage();
        var person = await PersonId(SqlServerFixture.SeededReportPerson);
        var condition = await ConditionId(SqlServerFixture.SeededReportCondition);

        try
        {
            using var promoted = await client.PostAsJsonAsync(
                $"/api/staged-messages/{staged}/case",
                new PromoteMessageRequest(
                    PersonId: person,
                    ConditionId: condition,
                    AgencyId: 1,
                    FirstReportedPhDate: new DateOnly(2026, 3, 12)),
                Json);

            Assert.Equal(HttpStatusCode.Created, promoted.StatusCode);

            var outcome = (await promoted.Content.ReadFromJsonAsync<PromoteMessageResponse>(Json))!;

            Assert.True(outcome.Report.Id > 0);
            Assert.Equal(person, outcome.Report.PersonId);
            Assert.Equal(condition, outcome.Report.ConditionId);
            Assert.Equal($"{year}00001", outcome.Report.RecordNumber);
            Assert.Equal(1, outcome.Report.AgencyId);

            // The batch notification the promotion raised: the line a reviewer
            // reads, naming the record number and the condition it was created
            // for.
            Assert.Contains($"{year}00001", outcome.Notification, StringComparison.Ordinal);
            Assert.Contains(SqlServerFixture.SeededReportCondition, outcome.Notification, StringComparison.Ordinal);
            Assert.Contains("notification has been raised", outcome.Notification, StringComparison.Ordinal);

            // The Location reads the created report back, so a promoted case is
            // a report like any other.
            Assert.Equal(
                $"/api/person-conditions/{outcome.Report.Id}",
                promoted.Headers.Location?.ToString());

            using var read = await client.GetAsync(promoted.Headers.Location);
            read.EnsureSuccessStatusCode();
            var reread = (await read.Content.ReadFromJsonAsync<PersonConditionResponse>(Json))!;
            Assert.Equal(outcome.Report, reread);

            // The message has left the review queue: it reads Assigned, the
            // terminal state the vocabulary now knows.
            Assert.Equal(MessageReviewStatus.Assigned, await ReviewStatusOf(staged));

            // And it records the case it became.
            await using (var context = server.NewContext())
            {
                Assert.Equal(
                    outcome.Report.Id,
                    await context.StagedMessages
                        .Where(message => message.Id == staged)
                        .Select(message => message.PersonConditionId)
                        .SingleAsync());
            }

            // A second promotion of the same message is refused, and no second
            // report is opened for it.
            using var again = await client.PostAsJsonAsync(
                $"/api/staged-messages/{staged}/case",
                new PromoteMessageRequest(person, condition),
                Json);

            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
            Assert.Equal("application/problem+json", again.Content.Headers.ContentType?.MediaType);

            using var problem = JsonDocument.Parse(await again.Content.ReadAsStringAsync());
            var root = problem.RootElement;
            Assert.Equal(409, root.GetProperty("status").GetInt32());
            Assert.Contains("already been promoted",
                root.GetProperty("detail").GetString(), StringComparison.Ordinal);

            Assert.Equal(1, await Reports());
        }
        finally
        {
            await Clear();
        }
    }

    /// <summary>
    /// Stages one message in the Entry state — the state intake leaves a
    /// parseable message in — and answers with its id. Written through the
    /// context, since staging is intake's and this suite starts after it.
    /// </summary>
    private async Task<int> StageMessage()
    {
        await using var context = server.NewContext();

        var message = new StagedMessage
        {
            RawMessage = "MSH|^~\\&|LAB|ARUP|SURVSYS|UT|20261017||ORU^R01|PROMOTE-IT|P|2.5.1",
            Source = "oru-promotion-test.hl7",
            ReceivedAt = DateTime.UtcNow,
            ReviewStatus = MessageReviewStatusRules.EntryCode,
            QaFlagged = false,
            ExceptionDetail = null,
        };

        context.StagedMessages.Add(message);
        await context.SaveChangesAsync();

        return message.Id;
    }

    /// <summary>The review state a staged message sits in, read back through the context.</summary>
    private async Task<MessageReviewStatus> ReviewStatusOf(int id)
    {
        await using var context = server.NewContext();

        var code = await context.StagedMessages
            .Where(message => message.Id == id)
            .Select(message => message.ReviewStatus)
            .SingleAsync();

        return MessageReviewStatusRules.Read(code);
    }

    /// <summary>
    /// The identifier of a seeded person, found over the wire by the name
    /// <see cref="SqlServerFixture.PersonsByName"/> writes them under.
    /// </summary>
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

    /// <summary>
    /// The identifier of a seeded condition, found over the wire by name.
    /// </summary>
    private async Task<int> ConditionId(string named)
    {
        using var response = await api.CreateClient().GetAsync("/api/conditions?pageSize=200");
        response.EnsureSuccessStatusCode();

        var page = (await response.Content
            .ReadFromJsonAsync<PagedResult<ConditionResponse>>(Json))!;

        return page.Items.Single(condition => condition.Name == named).Id;
    }

    private async Task<int> Reports()
    {
        await using var context = server.NewContext();

        return await context.PersonConditions.CountAsync();
    }

    /// <summary>
    /// Takes every report and every staged message out, so what record number
    /// the next promotion assigns and what the queue holds are facts about
    /// this test rather than about whichever ran before it.
    /// </summary>
    private async Task Clear()
    {
        await using var context = server.NewContext();

        await context.PersonConditions.ExecuteDeleteAsync();
        await context.StagedMessages.ExecuteDeleteAsync();
    }
}
