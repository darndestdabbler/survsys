using Survsys.Api;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The one decision the inbound ELR intake makes: how it classifies a raw
/// message into the staged message it queues.
/// </summary>
/// <remarks>
/// No host, no database and no file drop: that the worker polls a directory,
/// persists the message and moves the file aside is
/// <c>Survsys.IntegrationTests</c>'s to prove against a real host. What is here
/// is <see cref="ElrIntakeService.Stage"/> alone — a parseable message queued
/// for review in the Entry state, and a message that cannot be parsed held in
/// the Exception state with its detail — over the same ORU^R01 fixtures the
/// parser's own tests read.
/// </remarks>
public class ElrIntakeServiceTests
{
    private static readonly string FixtureRoot =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Hl7");

    private static string ReadFixture(string name) =>
        File.ReadAllText(Path.Combine(FixtureRoot, name));

    [Fact]
    public void StageQueuesAParseableMessageForReviewInTheEntryState()
    {
        var staged = ElrIntakeService.Stage(
            ReadFixture("oru-r01-chlamydia.hl7"), "oru-r01-chlamydia.hl7");

        Assert.Equal(MessageReviewStatusRules.EntryCode, staged.ReviewStatus);
        Assert.Equal(MessageReviewStatus.Entry, MessageReviewStatusRules.Read(staged.ReviewStatus));
        Assert.Null(staged.ExceptionDetail);
        Assert.False(staged.QaFlagged);
        Assert.Equal("oru-r01-chlamydia.hl7", staged.Source);
    }

    [Fact]
    public void StageHoldsAMalformedMessageInTheExceptionStateWithItsDetail()
    {
        var staged = ElrIntakeService.Stage(
            ReadFixture("oru-r01-malformed.hl7"), "oru-r01-malformed.hl7");

        Assert.Equal(MessageReviewStatusRules.ExceptionCode, staged.ReviewStatus);
        Assert.Equal(MessageReviewStatus.Exception, MessageReviewStatusRules.Read(staged.ReviewStatus));
        Assert.False(string.IsNullOrWhiteSpace(staged.ExceptionDetail));
    }

    /// <summary>
    /// Whichever state it lands in, the raw message is kept whole, so a
    /// reviewer reads what the lab sent rather than a reconstruction of it.
    /// </summary>
    [Fact]
    public void StageKeepsTheRawMessageWhole()
    {
        var raw = ReadFixture("oru-r01-chlamydia.hl7");

        var staged = ElrIntakeService.Stage(raw, "oru-r01-chlamydia.hl7");

        Assert.Equal(raw, staged.RawMessage);
    }
}
