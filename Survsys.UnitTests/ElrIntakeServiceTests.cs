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
        Assert.Null(staged.ExceptionCategory);
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
            Assert.Equal(MessageExceptionRules.UnknownSenderCode, message.ExceptionCategory);
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
        Assert.Equal(MessageExceptionRules.UnparseableCode, staged.ExceptionCategory);
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

    private const string CtSender = "CTLAB";

    private static readonly IReadOnlySet<string> CtSenders = new HashSet<string> { CtSender };

    private static ConformanceOptions CtOptions(string sender = CtSender) => new()
    {
        ConnecticutReceivingApplicationOid = "2.16.840.1.113883.3.5609.4.1.1.3.2.2",
        ConnecticutReceivingFacilityOid = "2.16.840.1.113883.3.5609.4.1",
        SenderProfiles = new() { [sender] = ConnecticutElrProfile.Name },
    };

    /// <summary>A Connecticut message conforming to the CT profile, with its OBR-17 callback number and PID-11 city given.</summary>
    private static string CtMessage(string callback = "^WPN^PH^^^860^9995661", string city = "Hartford") => string.Join('\r',
        "MSH|^~\\&|HealthSentry^2.16.840.1.113883.3.13.2.2.1^ISO|" + CtSender
            + "^07D0092913^CLIA|CT^2.16.840.1.113883.3.5609.4.1.1.3.2.2^ISO|CTA-DPH^2.16.840.1.113883.3.5609.4.1^ISO|20151004154300-0500||ORU^R01^ORU_R01|MSG1|T|2.5.1",
        "PID|1||999QQQ1234z^^^Lab&07D0092913&CLIA^MR||Patient^Test||19800115|F|||410 Capitol Avenue^^" + city + "^CT^06106^USA^H",
        "ORC|RE|PL1^EHR^07D0092913^CLIA|FL1^EHR^07D0092913^CLIA|||||||||^Anydoctor^Adam||^WPN^PH^^^860^9995661||||||||||100 Main St^^New Britain^CT^06051",
        "OBR|1|PL1^EHR^07D0092913^CLIA|FL1^EHR^07D0092913^CLIA|22327-1^Hepatitis C Antibody^LN|||20151003061900-0500|||||||||^Anydoctor^Adam|" + callback,
        "OBX|1|NM|22327-1^Hepatitis C Antibody^LN|1|31.8||||||F",
        "SPM|1|SP1^SP1&EHR&07D0092913&CLIA||119297000^Blood^SCT");

    [Fact]
    public void StageQueuesAMessageThatConformsToItsSendersProfile()
    {
        var staged = Assert.Single(ElrIntakeService.Stage(
            Parser, CtMessage(), null, CtSenders, CtOptions().ProfilesBySender()));

        Assert.Equal(MessageReviewStatusRules.EntryCode, staged.ReviewStatus);
        Assert.Null(staged.ExceptionCategory);
        Assert.Null(staged.ConformanceWarnings);
    }

    [Fact]
    public void StageKeepsThePatientsCounty()
    {
        var staged = Assert.Single(ElrIntakeService.Stage(
            Parser, CtMessage().Replace("^06106^USA^H", "^06106^USA^H^^09003"), null, CtSenders,
            CtOptions().ProfilesBySender()));

        Assert.Equal("09003", staged.PatientCounty);
    }

    [Fact]
    public void StageHoldsAMessageThatFailsARequiredCheckOfItsSendersProfile()
    {
        var staged = Assert.Single(ElrIntakeService.Stage(
            Parser, CtMessage(callback: ""), null, CtSenders, CtOptions().ProfilesBySender()));

        Assert.Equal(MessageReviewStatusRules.ExceptionCode, staged.ReviewStatus);
        Assert.Equal(MessageExceptionRules.ConformanceCode, staged.ExceptionCategory);
        Assert.Contains("OBR-17", staged.ExceptionDetail);
        Assert.Equal("22327-1", staged.TestCode);
    }

    [Fact]
    public void StageRecordsTheWarningsOfAMessageItQueues()
    {
        var staged = Assert.Single(ElrIntakeService.Stage(
            Parser, CtMessage(city: "Gotham"), null, CtSenders, CtOptions().ProfilesBySender()));

        Assert.Equal(MessageReviewStatusRules.EntryCode, staged.ReviewStatus);
        Assert.Contains("PID-11", staged.ConformanceWarnings);
    }

    [Fact]
    public void StageAcceptsATownGivenOnlyInTheTownsOnFile()
    {
        var towns = new HashSet<string>(["Gotham"], StringComparer.OrdinalIgnoreCase);

        var staged = Assert.Single(ElrIntakeService.Stage(
            Parser, CtMessage(city: "Gotham"), null, CtSenders, CtOptions().ProfilesBySender(towns)));

        Assert.Equal(MessageReviewStatusRules.EntryCode, staged.ReviewStatus);
        Assert.Null(staged.ConformanceWarnings);
    }

    [Fact]
    public void StageDoesNotCheckASenderWithNoProfile()
    {
        var staged = Assert.Single(ElrIntakeService.Stage(
            Parser, CtMessage(callback: "", city: "Gotham"), null, CtSenders, CtOptions("OTHERLAB").ProfilesBySender()));

        Assert.Equal(MessageReviewStatusRules.EntryCode, staged.ReviewStatus);
        Assert.Null(staged.ConformanceWarnings);
    }

    [Fact]
    public void ProfilesBySenderMapsEachSenderToItsProfileWithoutRegardToCase()
    {
        var profile = Assert.Single(CtOptions().ProfilesBySender()).Value;

        Assert.Equal(ConnecticutElrProfile.Name, profile.Name);
        Assert.True(CtOptions().ProfilesBySender().ContainsKey("ctlab"));
        Assert.Empty(new ConformanceOptions().ProfilesBySender());
    }

    [Fact]
    public void ProfilesBySenderRefusesAnUnknownProfileOrTheCtProfileWithoutItsOids()
    {
        var unknown = CtOptions();
        unknown.SenderProfiles[CtSender] = "NY";
        var withoutOids = new ConformanceOptions { SenderProfiles = new() { [CtSender] = ConnecticutElrProfile.Name } };

        Assert.Throws<InvalidOperationException>(() => unknown.ProfilesBySender());
        Assert.Throws<InvalidOperationException>(() => withoutOids.ProfilesBySender());
    }
}
