using Microsoft.EntityFrameworkCore;
using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Survsys.Messaging;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// A message written onto a case against the real database: each outcome of
/// comparing its lab with the lab an open case already holds, when
/// <see cref="MessageCaseWriter.Update"/> updates that case; a patient who is
/// not the case's person sent to pending; a promotion that fails part-way
/// leaving nothing behind; and a second promotion writing nothing.
/// </summary>
/// <remarks>
/// Each test seeds a person new to it, with an open case on the first
/// condition whose lab holds one preliminary positive test under LOINC 625-4,
/// accession ACC-1, collected five days after the case's event date — inside
/// the default whitelist rule's 90 days. Reports, labs and staged messages
/// are taken out before and after each test, as the other message suites do.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class MessageCaseWriterTests(SqlServerFixture server) : IAsyncLifetime
{
    private const int Preliminary = 677;
    private const int Final = 678;
    private const string Zip = "84101";

    private static readonly IMessageParser Parser = new OruR01Parser();
    private static readonly PersonMatchThresholds Thresholds = new();
    private static readonly DateOnly Collected = new(2026, 6, 10);

    public Task InitializeAsync() => Clear();

    public Task DisposeAsync() => Clear();

    [Fact]
    public async Task ALabTheCaseDoesNotHoldIsAdded()
    {
        var seeded = await Seed();
        var message = await Stage(seeded.LastName, "ACC-2", Result("6463-4", "Negative", "F"));

        var update = await Update(message, seeded);

        Assert.Equal(new CaseUpdate(MessageReviewStatusRules.AssignedCode, seeded.CaseId, null), update);

        await using var check = server.NewContext();
        var labs = await LabsOf(check, seeded.CaseId);
        Assert.Equal(2, labs.Count);
        var added = await check.LabTests.SingleAsync(test => test.LabId == labs[1]);
        Assert.Equal("6463-4", added.LoincCode);
        Assert.Equal(Final, added.TestStatusId);
        await AssertAssigned(check, message, seeded.CaseId);
    }

    [Fact]
    public async Task TheSameTestAndResultIsADuplicateAndWritesNoLab()
    {
        var seeded = await Seed();
        var message = await Stage(seeded.LastName, "ACC-1", Result("625-4", "Positive", "P"));

        var update = await Update(message, seeded);

        Assert.Equal(MessageReviewStatusRules.AssignedCode, update!.Status);

        await using var check = server.NewContext();
        Assert.Equal([seeded.LabId], await LabsOf(check, seeded.CaseId));
        Assert.Equal(1, await check.LabTests.CountAsync(test => test.LabId == seeded.LabId));
        Assert.Equal(Preliminary, (await check.LabTests.SingleAsync(test => test.LabId == seeded.LabId)).TestStatusId);
        await AssertAssigned(check, message, seeded.CaseId);
    }

    [Fact]
    public async Task AHigherTestStatusUpgradesTheStoredTest()
    {
        var seeded = await Seed();
        var message = await Stage(seeded.LastName, "ACC-1", Result("625-4", "Positive", "F"));

        await Update(message, seeded);

        await using var check = server.NewContext();
        Assert.Equal(Final, (await check.LabTests.SingleAsync(test => test.LabId == seeded.LabId)).TestStatusId);
        Assert.Equal([seeded.LabId], await LabsOf(check, seeded.CaseId));
        await AssertAssigned(check, message, seeded.CaseId);
    }

    [Fact]
    public async Task ATestTheLabLacksIsKnittedOntoIt()
    {
        var seeded = await Seed();
        var message = await Stage(
            seeded.LastName, "ACC-1", Result("625-4", "Positive", "P"), Result("6463-4", "Negative", "F", sequence: 2));

        await Update(message, seeded);

        await using var check = server.NewContext();
        Assert.Equal([seeded.LabId], await LabsOf(check, seeded.CaseId));
        var knitted = await check.LabTests.SingleAsync(test => test.LabId == seeded.LabId && test.LoincCode == "6463-4");
        Assert.Equal(
            "Negative",
            (await check.LabTestResults.SingleAsync(result => result.LabTestId == knitted.Id)).TestResult);
        await AssertAssigned(check, message, seeded.CaseId);
    }

    [Fact]
    public async Task AnotherResultForTheSameTestSendsTheMessageToPending()
    {
        var seeded = await Seed();
        var message = await Stage(seeded.LastName, "ACC-1", Result("625-4", "Negative", "F"));

        var update = await Update(message, seeded);

        Assert.Equal(MessageReviewStatusRules.PendingCode, update!.Status);

        await using var check = server.NewContext();
        var pending = await check.StagedMessages.SingleAsync(staged => staged.Id == message);
        Assert.Equal(MessageReviewStatusRules.PendingCode, pending.ReviewStatus);
        Assert.Equal(seeded.CaseId, pending.PersonConditionId);
        Assert.Contains("625-4", pending.ExceptionDetail, StringComparison.Ordinal);
        Assert.Equal(1, await check.LabTests.CountAsync(test => test.LabId == seeded.LabId));
    }

    [Fact]
    public async Task APatientWhoIsNotTheCasesPersonSendsTheMessageToPending()
    {
        var seeded = await Seed();
        var message = await Stage(Unique("STRANGER"), "ACC-2", Result("6463-4", "Negative", "F"));

        var update = await Update(message, seeded);

        Assert.Equal(MessageReviewStatusRules.PendingCode, update!.Status);
        Assert.Contains("not an exact match", update.Reason, StringComparison.Ordinal);

        await using var check = server.NewContext();
        Assert.Equal([seeded.LabId], await LabsOf(check, seeded.CaseId));
        Assert.Equal(
            MessageReviewStatusRules.PendingCode,
            (await check.StagedMessages.SingleAsync(staged => staged.Id == message)).ReviewStatus);
    }

    /// <summary>
    /// A result value the message holds but the lab's column does not — 500
    /// characters, where a message keeps 2000 and a lab result 200 — fails the
    /// write after the case is opened: the transaction leaves no new case, no
    /// new lab and the message waiting where it was.
    /// </summary>
    [Fact]
    public async Task APromotionThatFailsPartWayLeavesNothing()
    {
        var seeded = await Seed();
        var tooLong = Result("625-4", "Positive", "F");
        tooLong.ResultValue = new string('X', 500);
        var message = await Stage(seeded.LastName, "ACC-9", tooLong);

        await using (var context = server.NewContext())
        {
            await Assert.ThrowsAnyAsync<DbUpdateException>(() => MessageCaseWriter.Open(
                context, message, Creation(seeded), DateTimeOffset.Now, CancellationToken.None));
        }

        await using var check = server.NewContext();
        Assert.Equal(1, await check.PersonConditions.CountAsync(report => report.PersonId == seeded.PersonId));
        Assert.Equal(1, await check.Labs.CountAsync(lab => lab.PersonId == seeded.PersonId));
        var waiting = await check.StagedMessages.SingleAsync(staged => staged.Id == message);
        Assert.Equal(MessageReviewStatusRules.EntryCode, waiting.ReviewStatus);
        Assert.Null(waiting.PersonConditionId);
    }

    [Fact]
    public async Task ASecondPromotionOfAnAssignedMessageWritesNothing()
    {
        var seeded = await Seed();
        var message = await Stage(seeded.LastName, "ACC-3", Result("625-4", "Positive", "F"));

        await using (var context = server.NewContext())
        {
            var created = await MessageCaseWriter.Open(
                context, message, Creation(seeded), DateTimeOffset.Now, CancellationToken.None);
            Assert.NotNull(created);
        }

        await using (var context = server.NewContext())
        {
            Assert.Null(await MessageCaseWriter.Open(
                context, message, Creation(seeded), DateTimeOffset.Now, CancellationToken.None));
        }

        await using var check = server.NewContext();
        Assert.Equal(2, await check.PersonConditions.CountAsync(report => report.PersonId == seeded.PersonId));
        Assert.Equal(2, await check.Labs.CountAsync(lab => lab.PersonId == seeded.PersonId));
    }

    private sealed record Seeded(string LastName, int PersonId, int ConditionId, int CaseId, int LabId);

    /// <summary>A person new to the test with an open case holding one preliminary positive 625-4.</summary>
    private async Task<Seeded> Seed()
    {
        await using var context = server.NewContext();
        var conditionId = await context.Conditions.OrderBy(condition => condition.Id)
            .Select(condition => condition.Id)
            .FirstAsync();

        var lastName = Unique("UPDATE");
        var person = new Person { LastName = lastName, FirstName = "ANA", BirthDate = new DateOnly(1975, 6, 1), BirthGender = "F" };
        person.Addresses.Add(new PersonAddress { Street = "9 Elm St", PostalCode = Zip });
        context.Persons.Add(person);
        await context.SaveChangesAsync();

        var report = new PersonCondition
        {
            PersonId = person.Id,
            ConditionId = conditionId,
            RecordNumber = Unique("R"),
            EventOnsetDate = Collected.AddDays(-5),
            CreatedAt = DateTimeOffset.Now,
            WorkflowState = WorkflowRules.InitialCode,
        };
        context.PersonConditions.Add(report);
        await context.SaveChangesAsync();

        var lab = new Lab { PersonId = person.Id, PersonConditionId = report.Id, CollectionDate = Collected, AccessionNo = "ACC-1" };
        context.Labs.Add(lab);
        await context.SaveChangesAsync();

        var test = new LabTest { LabId = lab.Id, LoincCode = "625-4", TestStatusId = Preliminary };
        context.LabTests.Add(test);
        await context.SaveChangesAsync();

        context.LabTestResults.Add(new LabTestResult { LabTestId = test.Id, TestResult = "Positive", PrimaryLabResult = true });
        context.PersonConditionLabs.Add(new PersonConditionLab { PersonConditionId = report.Id, LabId = lab.Id });
        await context.SaveChangesAsync();

        return new Seeded(lastName, person.Id, conditionId, report.Id, lab.Id);
    }

    private static StagedMessageResult Result(string loinc, string interpretation, string status, int sequence = 1) => new()
    {
        Sequence = sequence,
        ObservationCode = loinc,
        Interpretation = interpretation,
        ResultStatus = status,
    };

    /// <summary>A message waiting in entry, for a patient of that last name, reporting these results.</summary>
    private async Task<int> Stage(string lastName, string accession, params StagedMessageResult[] results)
    {
        await using var context = server.NewContext();
        var conditionId = await context.Conditions.OrderBy(condition => condition.Id)
            .Select(condition => condition.Id)
            .FirstAsync();

        var message = new StagedMessage
        {
            RawMessage = "MSH|^~\\&|",
            Source = $"{lastName}-{accession}.hl7",
            ReceivedAt = DateTime.UtcNow,
            ReviewStatus = MessageReviewStatusRules.EntryCode,
            MessageType = "ORU^R01",
            PatientLastName = lastName,
            PatientFirstName = "ANA",
            PatientBirthDate = new DateOnly(1975, 6, 1),
            PatientSex = "F",
            PatientStreet = "9 Elm St",
            PatientZip = Zip,
            AccessionNo = accession,
            CollectedOn = Collected,
            ConditionId = conditionId,
            AllowNewCase = true,
            AllowUpdateCase = true,
            Results = [.. results],
        };
        context.StagedMessages.Add(message);
        await context.SaveChangesAsync();

        return message.Id;
    }

    private async Task<CaseUpdate?> Update(int message, Seeded seeded)
    {
        await using var context = server.NewContext();

        return await MessageCaseWriter.Update(
            context,
            Parser,
            Thresholds,
            new MessageProcessingOptions(),
            message,
            seeded.PersonId,
            null,
            DateTimeOffset.Now,
            CancellationToken.None);
    }

    private static CreatePersonConditionRequest Creation(Seeded seeded) =>
        new(seeded.PersonId, seeded.ConditionId, 1, Collected);

    private static Task<List<int>> LabsOf(SurvsysDbContext context, int caseId) =>
        context.PersonConditionLabs
            .Where(link => link.PersonConditionId == caseId)
            .OrderBy(link => link.LabId)
            .Select(link => link.LabId)
            .ToListAsync();

    private static async Task AssertAssigned(SurvsysDbContext context, int message, int caseId)
    {
        var assigned = await context.StagedMessages.SingleAsync(staged => staged.Id == message);
        Assert.Equal(MessageReviewStatusRules.AssignedCode, assigned.ReviewStatus);
        Assert.Equal(caseId, assigned.PersonConditionId);
        Assert.Null(assigned.ExceptionDetail);
    }

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private async Task Clear()
    {
        await using var context = server.NewContext();

        await context.PersonConditionLabs.ExecuteDeleteAsync();
        await context.Labs.ExecuteDeleteAsync();
        await context.PersonConditions.ExecuteDeleteAsync();
        await context.StagedMessages.ExecuteDeleteAsync();
    }
}
