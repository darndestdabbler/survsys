using Microsoft.EntityFrameworkCore;
using Survsys.Api;
using Survsys.Data;
using Survsys.Messaging;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// Message processing's passes against the real database: a message received
/// through <see cref="ElrIntakeService.Receive"/> and assigned to a new case by
/// <see cref="MessageProcessingService.Assign"/>; a graylisted message
/// <see cref="MessageProcessingService.Release"/> sends to a reviewer once a
/// case appears for its patient; and one <see cref="MessageProcessingService.Prune"/>
/// gives up on after its window.
/// </summary>
/// <remarks>
/// Every report and staged message is taken out before and after each test,
/// as the promotion tests do, so the passes see only what the test put there.
/// Each patient's last name is new to the test, so no person another test left
/// on file matches it.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class MessageProcessingServiceTests(SqlServerFixture server) : IAsyncLifetime
{
    private const string Sender = "TEST_LAB";
    private const string FixtureZip = "84101";

    private static readonly IMessageParser Parser = new OruR01Parser();
    private static readonly PersonMatchThresholds Thresholds = new();

    public Task InitializeAsync() => Clear();

    public Task DisposeAsync() => Clear();

    [Fact]
    public async Task AConfidentMessageIsAssignedToANewCaseWithoutAReviewer()
    {
        var facilityId = await EnsureSendingFacility();
        var zipAdded = await EnsureZipAgency();
        await using var vocabulary = await IntakeVocabulary.SeedAccepting(server, Sender);

        try
        {
            var lastName = Unique("ASSIGN");
            var raw = (await File.ReadAllTextAsync(
                    Path.Combine(AppContext.BaseDirectory, "Fixtures", "Hl7", "oru-r01-chlamydia.hl7")))
                .Replace("DOE^JOHN^A", $"{lastName}^JOHN^A", StringComparison.Ordinal);

            await using (var context = server.NewContext())
            {
                var staged = Assert.Single(
                    await ElrIntakeService.Receive(Parser, context, raw, "assign.hl7", CancellationToken.None));
                Assert.Equal(MessageReviewStatusRules.EntryCode, staged.ReviewStatus);
                Assert.True(staged.AllowNewCase);

                Assert.Equal(1, await MessageProcessingService.Assign(
                    context, Parser, Thresholds, new MessageProcessingOptions(), DateTimeOffset.Now,
                    CancellationToken.None));
            }

            await using var check = server.NewContext();
            var message = await check.StagedMessages.SingleAsync(staged => staged.Source == "assign.hl7");
            Assert.Equal(MessageReviewStatusRules.AssignedCode, message.ReviewStatus);
            Assert.Null(message.ExceptionDetail);

            var report = await check.PersonConditions.SingleAsync(report => report.Id == message.PersonConditionId);
            Assert.Equal(vocabulary.ConditionId, report.ConditionId);
            Assert.Equal(message.AgencyId, report.AgencyId);

            var person = await check.Persons.SingleAsync(person => person.Id == report.PersonId);
            Assert.Equal(lastName, person.LastName);
            Assert.Equal(new DateOnly(1980, 1, 15), person.BirthDate);

            // The message's specimen and its tests are written onto the case.
            var labId = await check.PersonConditionLabs
                .Where(link => link.PersonConditionId == report.Id)
                .Select(link => link.LabId)
                .SingleAsync();
            Assert.Equal(message.AccessionNo, (await check.Labs.SingleAsync(lab => lab.Id == labId)).AccessionNo);
            Assert.True(await check.LabTests.AnyAsync(test => test.LabId == labId));
        }
        finally
        {
            await using var context = server.NewContext();
            if (zipAdded)
            {
                await context.ZipCodeAgencies.Where(row => row.ZipCode == FixtureZip).ExecuteDeleteAsync();
            }
            await context.Facilities.Where(facility => facility.Id == facilityId).ExecuteDeleteAsync();
        }
    }

    /// <summary>
    /// A message whose assignment fails — a result value its lab column cannot
    /// hold — is held as a retryable exception rather than stopping the pass;
    /// once the value fits, reprocessing retries it, it is assigned, and its
    /// audit trail reads every state it passed through. A message held for a
    /// reason a person resolves is left alone.
    /// </summary>
    [Fact]
    public async Task AFailedAssignmentIsHeldRetriedAndAssignedWithItsTrail()
    {
        var facilityId = await EnsureSendingFacility();
        var zipAdded = await EnsureZipAgency();
        await using var vocabulary = await IntakeVocabulary.SeedAccepting(server, Sender);

        try
        {
            var lastName = Unique("RETRY");
            var raw = (await File.ReadAllTextAsync(
                    Path.Combine(AppContext.BaseDirectory, "Fixtures", "Hl7", "oru-r01-chlamydia.hl7")))
                .Replace("DOE^JOHN^A", $"{lastName}^RITA^A", StringComparison.Ordinal)
                .Replace("19800115|M", "19720304|F", StringComparison.Ordinal)
                .Replace("123 Main St", "77 Retry Rd", StringComparison.Ordinal);
            var options = new MessageProcessingOptions();
            string? received;
            int resultId;

            int messageId;
            await using (var context = server.NewContext())
            {
                var staged = Assert.Single(
                    await ElrIntakeService.Receive(Parser, context, raw, "retry.hl7", CancellationToken.None));
                messageId = staged.Id;
                received = staged.Results[0].ResultValue;
                resultId = staged.Results[0].Id;

                // Over the lab result's 200 characters, which the message's 2000 hold.
                await context.Set<StagedMessageResult>()
                    .Where(result => result.Id == resultId)
                    .ExecuteUpdateAsync(set => set.SetProperty(result => result.ResultValue, new string('X', 500)));

                context.StagedMessages.Add(new StagedMessage
                {
                    RawMessage = "MSH|^~\\&|",
                    Source = "person.hl7",
                    ReceivedAt = DateTime.UtcNow,
                    ReviewStatus = MessageReviewStatusRules.ExceptionCode,
                    ExceptionCategory = MessageExceptionRules.EntryQueueExceptionCode,
                    ExceptionDetail = "A person decides where this message goes.",
                    MessageType = "ORU^R01",
                });
                await context.SaveChangesAsync();
            }

            await using (var context = server.NewContext())
            {
                Assert.Equal(1, await MessageProcessingService.Assign(
                    context, Parser, Thresholds, options, DateTimeOffset.Now, CancellationToken.None));
            }

            await using (var check = server.NewContext())
            {
                var held = await check.StagedMessages.SingleAsync(message => message.Id == messageId);
                Assert.Equal(MessageReviewStatusRules.ExceptionCode, held.ReviewStatus);
                Assert.Equal(MessageExceptionRules.FailedToAddOrUpdateCode, held.ExceptionCategory);
                Assert.True(MessageExceptionRules.Read(held.ExceptionCategory).Retryable);
                Assert.Null(held.PersonConditionId);
                Assert.False(await check.Persons.AnyAsync(person => person.LastName == lastName));

                await check.Set<StagedMessageResult>()
                    .Where(result => result.Id == resultId)
                    .ExecuteUpdateAsync(set => set.SetProperty(result => result.ResultValue, received));
            }

            await using (var context = server.NewContext())
            {
                Assert.Equal(1, await MessageProcessingService.Reprocess(context, options, CancellationToken.None));
                Assert.Equal(1, await MessageProcessingService.Assign(
                    context, Parser, Thresholds, options, DateTimeOffset.Now, CancellationToken.None));
            }

            await using var final = server.NewContext();
            var assigned = await final.StagedMessages.SingleAsync(message => message.Id == messageId);
            Assert.Equal(MessageReviewStatusRules.AssignedCode, assigned.ReviewStatus);
            Assert.Null(assigned.ExceptionCategory);
            Assert.Equal(1, assigned.RetryCount);
            Assert.NotNull(assigned.PersonConditionId);

            var trail = await final.StagedMessageAudits
                .Where(audit => audit.StagedMessageId == messageId)
                .OrderBy(audit => audit.OccurredAt)
                .ThenBy(audit => audit.Id)
                .ToListAsync();
            Assert.Equal(
                [
                    MessageReviewStatusRules.EntryCode,
                    MessageReviewStatusRules.ExceptionCode,
                    MessageReviewStatusRules.EntryCode,
                    MessageReviewStatusRules.AssignedCode,
                ],
                trail.Select(audit => audit.ReviewStatus));
            Assert.Equal(MessageExceptionRules.FailedToAddOrUpdateCode, trail[1].ExceptionCategory);
            Assert.Contains("failed", trail[1].ExceptionDetail, StringComparison.Ordinal);

            var untouched = await final.StagedMessages.SingleAsync(message => message.Source == "person.hl7");
            Assert.Equal(MessageReviewStatusRules.ExceptionCode, untouched.ReviewStatus);
            Assert.Equal(0, untouched.RetryCount);
        }
        finally
        {
            await using var context = server.NewContext();
            if (zipAdded)
            {
                await context.ZipCodeAgencies.Where(row => row.ZipCode == FixtureZip).ExecuteDeleteAsync();
            }
            await context.Facilities.Where(facility => facility.Id == facilityId).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task AGraylistedMessageIsReleasedWhenACaseAppearsForItsPatient()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var lastName = Unique("GRAY");

        await using var context = server.NewContext();
        var conditionId = await context.Conditions.OrderBy(condition => condition.Id)
            .Select(condition => condition.Id)
            .FirstAsync();

        var person = new Person { LastName = lastName, FirstName = "ANA", BirthDate = new DateOnly(1975, 6, 1), BirthGender = "F" };
        person.Addresses.Add(new PersonAddress { Street = "9 Elm St", PostalCode = FixtureZip });
        context.Persons.Add(person);
        await context.SaveChangesAsync();

        var waiting = Graylisted(lastName, conditionId, today.AddDays(-5), DateTime.UtcNow);
        var stranger = Graylisted(Unique("OTHER"), conditionId, today.AddDays(-5), DateTime.UtcNow);
        context.StagedMessages.AddRange(waiting, stranger);
        await context.SaveChangesAsync();

        // Before a case appears there is nothing to release.
        Assert.Equal(0, await MessageProcessingService.Release(context, Parser, Thresholds, CancellationToken.None));

        var report = new PersonCondition
        {
            PersonId = person.Id,
            ConditionId = conditionId,
            RecordNumber = "2026GRAY01",
            EventOnsetDate = today,
            CreatedAt = DateTimeOffset.Now,
            WorkflowState = WorkflowRules.InitialCode,
        };
        context.PersonConditions.Add(report);
        await context.SaveChangesAsync();

        Assert.Equal(1, await MessageProcessingService.Release(context, Parser, Thresholds, CancellationToken.None));

        await using var check = server.NewContext();
        var released = await check.StagedMessages.SingleAsync(message => message.Id == waiting.Id);
        Assert.Equal(MessageReviewStatusRules.PendingCode, released.ReviewStatus);
        Assert.Equal(report.Id, released.PersonConditionId);
        Assert.Contains("2026GRAY01", released.ExceptionDetail, StringComparison.Ordinal);

        var unmatched = await check.StagedMessages.SingleAsync(message => message.Id == stranger.Id);
        Assert.Equal(MessageReviewStatusRules.GrayCode, unmatched.ReviewStatus);
        Assert.Null(unmatched.PersonConditionId);
    }

    [Fact]
    public async Task AGraylistedMessagePastItsWindowIsPruned()
    {
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        await using var context = server.NewContext();
        var conditionId = await context.Conditions.OrderBy(condition => condition.Id)
            .Select(condition => condition.Id)
            .FirstAsync();

        var old = Graylisted(Unique("OLD"), conditionId, today.AddDays(-600), now.UtcDateTime.AddDays(-600));
        var recent = Graylisted(Unique("NEW"), conditionId, today.AddDays(-10), now.UtcDateTime.AddDays(-10));
        context.StagedMessages.AddRange(old, recent);
        await context.SaveChangesAsync();

        Assert.Equal(1, await MessageProcessingService.Prune(
            context, new MessageProcessingOptions(), now, CancellationToken.None));

        await using var check = server.NewContext();
        var pruned = await check.StagedMessages.SingleAsync(message => message.Id == old.Id);
        Assert.Equal(MessageReviewStatusRules.GrayUnprocessableCode, pruned.ReviewStatus);
        Assert.Equal(
            MessageReviewStatusRules.GrayCode,
            (await check.StagedMessages.SingleAsync(message => message.Id == recent.Id)).ReviewStatus);
    }

    /// <summary>
    /// A graylisted message for a patient at the address the release test's
    /// person lives at, collected and received when given.
    /// </summary>
    private static StagedMessage Graylisted(string lastName, int conditionId, DateOnly collectedOn, DateTime receivedAt) =>
        new()
        {
            RawMessage = "MSH|^~\\&|",
            Source = $"{lastName}.hl7",
            ReceivedAt = receivedAt,
            ReviewStatus = MessageReviewStatusRules.GrayCode,
            MessageType = "ORU^R01",
            SendingFacility = Sender,
            PatientLastName = lastName,
            PatientFirstName = "ANA",
            PatientBirthDate = new DateOnly(1975, 6, 1),
            PatientSex = "F",
            PatientStreet = "9 Elm St",
            PatientZip = FixtureZip,
            CollectedOn = collectedOn,
            ConditionId = conditionId,
            AllowNewCase = false,
        };

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    /// <summary>
    /// Puts the fixture's sender on file unless it already is, answering the
    /// id of the facility it added, or null where it added none.
    /// </summary>
    private async Task<int?> EnsureSendingFacility()
    {
        await using var context = server.NewContext();

        if (await context.Facilities.AnyAsync(facility => facility.ShortName == Sender))
        {
            return null;
        }

        var facility = new Facility { Name = "Message processing test lab", ShortName = Sender, AgencyId = 1 };
        context.Facilities.Add(facility);
        await context.SaveChangesAsync();

        return facility.Id;
    }

    /// <summary>
    /// Maps the fixture patient's zip to an agency unless it already is mapped,
    /// answering whether it added the mapping.
    /// </summary>
    private async Task<bool> EnsureZipAgency()
    {
        await using var context = server.NewContext();

        if (await context.ZipCodeAgencies.AnyAsync(row => row.ZipCode == FixtureZip))
        {
            return false;
        }

        context.ZipCodeAgencies.Add(new ZipCodeAgency { ZipCode = FixtureZip, AgencyId = 1 });
        await context.SaveChangesAsync();

        return true;
    }

    private async Task Clear()
    {
        await using var context = server.NewContext();

        // An assigned message writes its lab onto the case, so the labs go
        // before the cases they are linked to.
        await context.PersonConditionLabs.ExecuteDeleteAsync();
        await context.Labs.ExecuteDeleteAsync();
        await context.PersonConditions.ExecuteDeleteAsync();
        await context.StagedMessages.ExecuteDeleteAsync();
    }
}
