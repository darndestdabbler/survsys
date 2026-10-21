using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Survsys.Api;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The batch notification against the real database: an assigned message is
/// sent to its case's agency and marked notified, a message whose agency has no
/// addresses is neither sent nor marked, and a send that fails marks nothing.
/// </summary>
/// <remarks>
/// The mail server is the send delegate, which records what it is handed or
/// throws, so nothing leaves the machine. Every case and staged message is
/// taken out before and after each test, and the agencies and person the test
/// adds after it.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class MessageNotificationServiceTests(SqlServerFixture server) : IAsyncLifetime
{
    private const string From = "survsys@example.org";

    private static readonly DateTime Now = new(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);

    private readonly List<int> agencies = [];

    private int? personId;

    public Task InitializeAsync() => Clear();

    public async Task DisposeAsync()
    {
        await Clear();

        await using var context = server.NewContext();
        await context.Agencies.Where(agency => agencies.Contains(agency.Id)).ExecuteDeleteAsync();
        await context.Persons.Where(person => person.Id == personId).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task AnAssignedMessageIsSentToItsAgencyAndMarkedNotified()
    {
        var notified = await Agency("epi@example.org; lab@example.org");
        var silent = await Agency(null);
        var sentMessage = await Assigned(notified, "2026NOTE01");
        var unsentMessage = await Assigned(silent, "2026NOTE02");
        var sent = new List<(string To, string Subject, string Body)>();

        await using (var context = server.NewContext())
        {
            Assert.Equal(1, await MessageNotificationService.Notify(
                context,
                From,
                (mail, _) =>
                {
                    sent.Add((mail.To.ToString(), mail.Subject, mail.Body));
                    return Task.CompletedTask;
                },
                Now,
                CancellationToken.None));
        }

        var (to, subject, body) = Assert.Single(sent);
        Assert.Equal("epi@example.org, lab@example.org", to);
        Assert.Contains("1 laboratory message(s)", subject, StringComparison.Ordinal);
        Assert.Contains("2026NOTE01", body, StringComparison.Ordinal);
        Assert.DoesNotContain("2026NOTE02", body, StringComparison.Ordinal);

        await using var check = server.NewContext();
        Assert.Equal(Now, (await check.StagedMessages.SingleAsync(message => message.Id == sentMessage)).NotifiedAt);

        // No one was told of the message whose agency has no addresses, so it
        // is not marked notified.
        Assert.Null((await check.StagedMessages.SingleAsync(message => message.Id == unsentMessage)).NotifiedAt);

        // The sent message is marked, and the other's agency still has no
        // addresses, so the next pass sends nothing.
        Assert.Equal(0, await MessageNotificationService.Notify(
            check, From, (_, _) => throw new InvalidOperationException("Nothing should be sent."), Now, CancellationToken.None));
    }

    [Fact]
    public async Task ASendThatFailsLeavesItsMessagesUnnotified()
    {
        var agencyId = await Agency("epi@example.org");
        var messageId = await Assigned(agencyId, "2026NOTE03");

        await using (var context = server.NewContext())
        {
            await Assert.ThrowsAsync<SmtpException>(() => MessageNotificationService.Notify(
                context, From, (_, _) => throw new SmtpException("The server refused the message."), Now, CancellationToken.None));
        }

        await using var check = server.NewContext();
        Assert.Null((await check.StagedMessages.SingleAsync(message => message.Id == messageId)).NotifiedAt);
    }

    private async Task<int> Agency(string? notificationEmails)
    {
        await using var context = server.NewContext();

        var agency = new Agency { Name = $"Notification test {Guid.NewGuid():N}"[..40], NotificationEmails = notificationEmails };
        context.Agencies.Add(agency);
        await context.SaveChangesAsync();
        agencies.Add(agency.Id);

        return agency.Id;
    }

    /// <summary>A message assigned to a new case of the test's person, under the agency given.</summary>
    private async Task<int> Assigned(int agencyId, string recordNumber)
    {
        await using var context = server.NewContext();

        if (personId is null)
        {
            var person = new Person { LastName = "NOTIFY", FirstName = "NORA", BirthDate = new DateOnly(1990, 2, 3) };
            context.Persons.Add(person);
            await context.SaveChangesAsync();
            personId = person.Id;
        }

        var report = new PersonCondition
        {
            PersonId = personId.Value,
            ConditionId = await context.Conditions.OrderBy(condition => condition.Id)
                .Select(condition => condition.Id)
                .FirstAsync(),
            AgencyId = agencyId,
            RecordNumber = recordNumber,
            CreatedAt = DateTimeOffset.Now,
            WorkflowState = WorkflowRules.InitialCode,
        };
        context.PersonConditions.Add(report);
        await context.SaveChangesAsync();

        var message = new StagedMessage
        {
            RawMessage = "MSH|^~\\&|",
            Source = "notify.hl7",
            ReceivedAt = Now.AddHours(-1),
            ReviewStatus = MessageReviewStatusRules.AssignedCode,
            PersonConditionId = report.Id,
        };
        context.StagedMessages.Add(message);
        await context.SaveChangesAsync();

        return message.Id;
    }

    private async Task Clear()
    {
        await using var context = server.NewContext();

        await context.PersonConditionLabs.ExecuteDeleteAsync();
        await context.Labs.ExecuteDeleteAsync();
        await context.PersonConditions.ExecuteDeleteAsync();
        await context.StagedMessages.ExecuteDeleteAsync();
    }
}
