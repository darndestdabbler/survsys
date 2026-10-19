using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The inbound ELR intake end to end, against the real host and database: a
/// message from a sending facility on file carried from a dropped file to
/// queued-for-review with its parsed fields, and a message that cannot be
/// parsed or comes from a sender not on file held as an exception.
/// </summary>
/// <remarks>
/// The host is the real <c>Program.cs</c> started as Production, with its
/// intake pointed at a fresh temporary drop directory through the
/// <c>ElrIntake</c> configuration — set in the environment before the host
/// builds, as the connection string is — and polling once a second so the test
/// does not wait on the production interval. The review states asserted are the
/// legacy EMSA queue states <c>../nedss/docs/db-snapshots/session-013/</c>
/// dumps on <c>system_statuses</c> — Entry for a received message, Exception
/// for one that failed to process — as <see cref="SqlServerFixture"/>'s seeded
/// record number echoes session-011. The suite does not skip when SQL Server is
/// absent; <see cref="SqlServerFixture"/> fails it loudly instead.
///
/// This proves the message is <em>queued</em>, not promoted: it asserts the
/// staged row and the raw text read back, and no person, lab or case, because
/// queuing for review creates none.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class ElrIntakeTests(SqlServerFixture server)
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task ADroppedMessageIsCarriedToQueuedForReviewAndAnUnparseableOneToException()
    {
        var drop = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "survsys-elr-" + Guid.NewGuid().ToString("N")));

        Environment.SetEnvironmentVariable("ElrIntake__DropDirectory", drop.FullName);
        Environment.SetEnvironmentVariable("ElrIntake__PollIntervalSeconds", "1");

        // The fixture's sender, MSH-4.1 TEST_LAB, put on file as a facility's
        // short name, so its message is queued rather than held.
        var senderId = await EnsureSendingFacility("TEST_LAB");

        try
        {
            // The valid message is dropped before the host starts, so the
            // intake's first poll finds it.
            const string validFile = "oru-r01-chlamydia.hl7";
            var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Hl7", validFile);
            var validRaw = await File.ReadAllTextAsync(fixturePath);
            File.Copy(fixturePath, Path.Combine(drop.FullName, validFile));

            using var api = new SurvsysApi(server.ConnectionString);
            using var client = api.CreateClient();

            var queued = await WaitForStaged(validFile);
            Assert.NotNull(queued);
            Assert.Equal(MessageReviewStatusRules.EntryCode, queued!.ReviewStatus);
            Assert.Equal(MessageReviewStatus.Entry, MessageReviewStatusRules.Read(queued.ReviewStatus));
            Assert.Equal(validFile, queued.Source);
            Assert.Equal(validRaw, queued.RawMessage);
            Assert.Null(queued.ExceptionDetail);

            // The parsed fields are kept on the row, with the order's results
            // beside it.
            Assert.Equal("HL7v2", queued.MessageFormat);
            Assert.Equal("ORU^R01", queued.MessageType);
            Assert.Equal("TEST_LAB", queued.SendingFacility);
            Assert.Equal("DOE", queued.PatientLastName);
            Assert.Equal(new DateOnly(1980, 1, 15), queued.PatientBirthDate);
            Assert.Equal("11585-7", queued.TestCode);
            Assert.Equal("119364003", queued.SpecimenSourceCode);
            var result = Assert.Single(queued.Results);
            Assert.Equal("260373001", result.ResultValue);
            Assert.Equal("Detected", result.CodedResult);

            // The same message read back through the wire: the detail endpoint
            // carries the raw text and the Entry state, which is the stack end
            // to end.
            var detail = await client.GetFromJsonAsync<StagedMessageDetailResponse>(
                $"/api/staged-messages/{queued.Id}");
            Assert.NotNull(detail);
            Assert.Equal(validRaw, detail!.RawMessage);
            Assert.Equal(MessageReviewStatusRules.EntryCode, detail.Message.ReviewStatus);

            // A message that cannot be parsed, dropped once the host is running,
            // is held as an exception with the parser's account of why.
            const string badFile = "unparseable.hl7";
            await File.WriteAllTextAsync(
                Path.Combine(drop.FullName, badFile), "this is not a valid HL7 v2 message");

            var excepted = await WaitForStaged(badFile);
            Assert.NotNull(excepted);
            Assert.Equal(MessageReviewStatusRules.ExceptionCode, excepted!.ReviewStatus);
            Assert.Equal(
                MessageReviewStatus.Exception, MessageReviewStatusRules.Read(excepted.ReviewStatus));
            Assert.False(string.IsNullOrWhiteSpace(excepted.ExceptionDetail));

            // A parseable message from a sender not on file is held as an
            // exception naming the sender.
            const string unknownFile = "unknown-sender.hl7";
            await File.WriteAllTextAsync(
                Path.Combine(drop.FullName, unknownFile),
                validRaw.Replace("|TEST_LAB^2.16", "|UNKNOWN_LAB^2.16", StringComparison.Ordinal));

            var held = await WaitForStaged(unknownFile);
            Assert.NotNull(held);
            Assert.Equal(MessageReviewStatusRules.ExceptionCode, held!.ReviewStatus);
            Assert.Equal("UNKNOWN_LAB", held.SendingFacility);
            Assert.Contains("UNKNOWN_LAB", held.ExceptionDetail);
        }
        finally
        {
            await RemoveSendingFacility(senderId);
            Environment.SetEnvironmentVariable("ElrIntake__DropDirectory", null);
            Environment.SetEnvironmentVariable("ElrIntake__PollIntervalSeconds", null);
            TryDeleteDirectory(drop.FullName);
        }
    }

    /// <summary>
    /// Waits, with a bounded patience, for the intake to stage the message
    /// dropped under this file name, reading the database directly rather than
    /// the endpoint so the wait is on the worker and not on a client.
    /// </summary>
    private async Task<StagedMessage?> WaitForStaged(string source)
    {
        var deadline = DateTime.UtcNow + Patience;

        while (DateTime.UtcNow < deadline)
        {
            await using var context = server.NewContext();

            var found = await context.StagedMessages
                .AsNoTracking()
                .Include(message => message.Results)
                .Where(message => message.Source == source)
                .OrderByDescending(message => message.Id)
                .FirstOrDefaultAsync();

            if (found is not null)
            {
                return found;
            }

            await Task.Delay(250);
        }

        return null;
    }

    /// <summary>
    /// Puts a facility with this short name on file unless one already is,
    /// answering the id of the one it added, or null where it added none.
    /// </summary>
    private async Task<int?> EnsureSendingFacility(string shortName)
    {
        await using var context = server.NewContext();

        if (await context.Facilities.AnyAsync(facility => facility.ShortName == shortName))
        {
            return null;
        }

        var facility = new Facility { Name = "ELR intake test lab", ShortName = shortName, AgencyId = 1 };
        context.Facilities.Add(facility);
        await context.SaveChangesAsync();

        return facility.Id;
    }

    private async Task RemoveSendingFacility(int? facilityId)
    {
        if (facilityId is null)
        {
            return;
        }

        await using var context = server.NewContext();
        await context.Facilities.Where(facility => facility.Id == facilityId).ExecuteDeleteAsync();
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            // A temporary directory the operating system will reclaim; a file
            // still held open is not this test's failure to report.
        }
    }
}
