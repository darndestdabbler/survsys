using Survsys.Api;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The one decision the inbound ELR intake makes: how it classifies a raw
/// message into the staged messages it queues.
/// </summary>
/// <remarks>
/// No host, no database and no file drop: that the worker polls a directory,
/// persists the message and moves the file aside is
/// <c>Survsys.IntegrationTests</c>'s to prove against a real host. What is here
/// is <see cref="ElrIntakeService.Stage"/> alone — a parseable message queued
/// for review in the Entry state with its parsed fields, one staged message
/// per OBR, and a message that cannot be parsed or comes from a sender not on
/// file held in the Exception state with its detail — over the same ORU^R01
/// fixtures the parser's own tests read.
/// </remarks>
public class ElrIntakeServiceTests
{
    private static readonly string FixtureRoot =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Hl7");

    private static string ReadFixture(string name) =>
        File.ReadAllText(Path.Combine(FixtureRoot, name));

    private static readonly IReadOnlySet<string> KnownSenders = new HashSet<string> { "TEST_LAB" };

    [Fact]
    public void StageQueuesAParseableMessageForReviewInTheEntryState()
    {
        var staged = Assert.Single(ElrIntakeService.Stage(
            ReadFixture("oru-r01-chlamydia.hl7"), "oru-r01-chlamydia.hl7", KnownSenders));

        Assert.Equal(MessageReviewStatusRules.EntryCode, staged.ReviewStatus);
        Assert.Equal(MessageReviewStatus.Entry, MessageReviewStatusRules.Read(staged.ReviewStatus));
        Assert.Null(staged.ExceptionDetail);
        Assert.False(staged.QaFlagged);
        Assert.Equal("oru-r01-chlamydia.hl7", staged.Source);
    }

    [Fact]
    public void StageKeepsTheParsedFields()
    {
        var staged = Assert.Single(ElrIntakeService.Stage(
            ReadFixture("oru-r01-chlamydia.hl7"), "oru-r01-chlamydia.hl7", KnownSenders));

        Assert.Equal("HL7v2", staged.MessageFormat);
        Assert.Equal("ORU^R01", staged.MessageType);
        Assert.Equal("TEST_LAB", staged.SendingFacility);
        Assert.Equal("DOE", staged.PatientLastName);
        Assert.Equal(new DateOnly(1980, 1, 15), staged.PatientBirthDate);
        Assert.Equal("84101", staged.PatientZip);
        Assert.Equal(0, staged.OrderIndex);
        Assert.Equal("11585-7", staged.TestCode);
        Assert.Equal("119364003", staged.SpecimenSourceCode);
        var result = Assert.Single(staged.Results);
        Assert.Equal("Detected", result.CodedResult);
    }

    [Fact]
    public void StageSplitsAMessageIntoOneStagedMessagePerObr()
    {
        var raw = ReadFixture("oru-r01-two-orders.hl7");

        var staged = ElrIntakeService.Stage(raw, "oru-r01-two-orders.hl7", KnownSenders);

        Assert.Collection(
            staged,
            first =>
            {
                Assert.Equal(0, first.OrderIndex);
                Assert.Equal("11585-7", first.TestCode);
                Assert.Single(first.Results);
            },
            second =>
            {
                Assert.Equal(1, second.OrderIndex);
                Assert.Equal("5196-1", second.TestCode);
                Assert.Equal(2, second.Results.Count);
            });
        Assert.All(staged, message =>
        {
            Assert.Equal(raw, message.RawMessage);
            Assert.Equal("ROE", message.PatientLastName);
            Assert.Equal(MessageReviewStatusRules.EntryCode, message.ReviewStatus);
        });
    }

    [Fact]
    public void StageHoldsAMessageFromAnUnknownSenderInTheExceptionState()
    {
        var staged = ElrIntakeService.Stage(
            ReadFixture("oru-r01-two-orders.hl7"), "oru-r01-two-orders.hl7", new HashSet<string>());

        Assert.Equal(2, staged.Count);
        Assert.All(staged, message =>
        {
            Assert.Equal(MessageReviewStatusRules.ExceptionCode, message.ReviewStatus);
            Assert.Contains("TEST_LAB", message.ExceptionDetail);
            Assert.Contains("MSH-4", message.ExceptionDetail);
        });
    }

    [Fact]
    public void StageHoldsAMalformedMessageInTheExceptionStateWithItsDetail()
    {
        var staged = Assert.Single(ElrIntakeService.Stage(
            ReadFixture("oru-r01-malformed.hl7"), "oru-r01-malformed.hl7", KnownSenders));

        Assert.Equal(MessageReviewStatusRules.ExceptionCode, staged.ReviewStatus);
        Assert.Equal(MessageReviewStatus.Exception, MessageReviewStatusRules.Read(staged.ReviewStatus));
        Assert.False(string.IsNullOrWhiteSpace(staged.ExceptionDetail));
        Assert.Null(staged.OrderIndex);
    }

    /// <summary>
    /// Whichever state it lands in, the raw message is kept whole, so a
    /// reviewer reads what the lab sent rather than a reconstruction of it.
    /// </summary>
    [Fact]
    public void StageKeepsTheRawMessageWhole()
    {
        var raw = ReadFixture("oru-r01-chlamydia.hl7");

        var staged = Assert.Single(ElrIntakeService.Stage(raw, "oru-r01-chlamydia.hl7", KnownSenders));

        Assert.Equal(raw, staged.RawMessage);
    }
}
