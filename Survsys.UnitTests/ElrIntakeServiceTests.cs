using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Survsys.Messaging;
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

    private static readonly IMessageParser Parser = new OruR01Parser();

    private static readonly IReadOnlySet<string> KnownSenders = new HashSet<string> { "TEST_LAB" };

    [Fact]
    public void StageQueuesAParseableMessageForReviewInTheEntryState()
    {
        var staged = Assert.Single(ElrIntakeService.Stage(
            Parser,
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
            Parser,
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

        var staged = ElrIntakeService.Stage(Parser, raw, "oru-r01-two-orders.hl7", KnownSenders);

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
    public void StageGivesEachObrOfEachPatientItsOwnStagedMessageWithThatPatient()
    {
        var staged = ElrIntakeService.Stage(
            Parser, ReadFixture("oru-r01-two-patients.hl7"), "oru-r01-two-patients.hl7", KnownSenders);

        Assert.Collection(
            staged,
            first =>
            {
                Assert.Equal(0, first.OrderIndex);
                Assert.Equal("POE", first.PatientLastName);
                Assert.Equal("06510", first.PatientZip);
                Assert.Equal("OBR004", first.AccessionNo);
            },
            second =>
            {
                Assert.Equal(1, second.OrderIndex);
                Assert.Equal("LOE", second.PatientLastName);
                Assert.Equal("06901", second.PatientZip);
                Assert.Equal("OBR005", second.AccessionNo);
            });
        Assert.All(staged, message => Assert.Equal(MessageReviewStatusRules.EntryCode, message.ReviewStatus));
    }

    /// <summary>
    /// Stage reads the message through the parser it is given, whatever that
    /// parser is: what a parser of another format returns is what is staged.
    /// </summary>
    [Fact]
    public void StageStagesWhatTheParserItIsGivenReturns()
    {
        var staged = Assert.Single(ElrIntakeService.Stage(
            new FixedParser(), "not read by this parser", "fixed", KnownSenders));

        Assert.Equal("TEST", staged.MessageFormat);
        Assert.Equal("DOC^01", staged.MessageType);
        Assert.Equal("STUB", staged.PatientLastName);
        Assert.Equal("ACC-1", staged.AccessionNo);
        Assert.Equal("Positive", Assert.Single(staged.Results).ResultValue);
        Assert.Equal(MessageReviewStatusRules.EntryCode, staged.ReviewStatus);
    }

    [Fact]
    public void StageHoldsAMessageFromAnUnknownSenderInTheExceptionState()
    {
        var staged = ElrIntakeService.Stage(
            Parser,
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
            Parser,
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

        var staged = Assert.Single(ElrIntakeService.Stage(Parser, raw, "oru-r01-chlamydia.hl7", KnownSenders));

        Assert.Equal(raw, staged.RawMessage);
    }

    /// <summary>A parser of no real format, answering one fixed message whatever it is given.</summary>
    private sealed class FixedParser : IMessageParser
    {
        public string Format => "TEST";

        public ParsedMessage Parse(string raw) => new(
            Format,
            "DOC^01",
            "TEST_LAB",
            [
                new ParsedPatient(
                    new CreatePersonRequest("STUB"),
                    null,
                    null,
                    null,
                    null,
                    [
                        new ParsedOrder(
                            "ACC-1",
                            "1234-5",
                            null,
                            null,
                            null,
                            null,
                            null,
                            [new ParsedResult("1234-5", null, "Positive", null, null, null, null, "F")]),
                    ]),
            ]);
    }
}
