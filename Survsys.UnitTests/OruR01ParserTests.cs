using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The parser read over fixtures derived from the ORU^R01 message documented
/// under ../nedss/docs/: the Chlamydia result it must land into the fields
/// intake keeps, a message of two orders it must split, and a malformed
/// message it must refuse.
/// </summary>
public class OruR01ParserTests
{
    private static readonly string FixtureRoot =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Hl7");

    private static string ReadFixture(string name) =>
        File.ReadAllText(Path.Combine(FixtureRoot, name));

    [Fact]
    public void ParsesTheDocumentedChlamydiaResultIntoTheFieldsIntakeKeeps()
    {
        var result = new OruR01Parser().Parse(ReadFixture("oru-r01-chlamydia.hl7"));

        Assert.Equal(OruR01Parser.Hl7V2Format, result.Format);
        Assert.Equal("ORU^R01", result.MessageType);
        Assert.Equal("TEST_LAB", result.SendingFacility);

        Assert.Equal("DOE", result.Person.LastName);
        Assert.Equal("JOHN", result.Person.FirstName);
        Assert.Equal("A", result.Person.MiddleName);
        Assert.Equal(new DateOnly(1980, 1, 15), result.Person.BirthDate);
        Assert.Equal("M", result.Person.BirthGender);
        Assert.Equal("123 Main St", result.Street);
        Assert.Equal("Salt Lake City", result.City);
        Assert.Equal("UT", result.State);
        Assert.Equal("84101", result.Zip);

        var order = Assert.Single(result.Orders);
        Assert.Equal("OBR001", order.AccessionNo);
        Assert.Equal("11585-7", order.TestCode);
        Assert.Equal("Chlamydia trachomatis Ag [Presence] in Specimen", order.TestName);
        Assert.Equal(new DateOnly(2026, 3, 12), order.ObservedOn);
        Assert.Equal("119364003", order.SpecimenCode);
        Assert.Equal("Serum specimen", order.SpecimenText);
        // No SPM-17 on this message, so the collection date falls back to OBR-7.
        Assert.Equal(new DateOnly(2026, 3, 12), order.CollectedOn);

        var labResult = Assert.Single(order.Results);
        Assert.Equal("11585-7", labResult.ObservationCode);
        Assert.Equal("260373001", labResult.ResultValue);
        Assert.Equal("Detected", labResult.CodedResult);
        Assert.Equal("A", labResult.AbnormalFlag);
        Assert.Equal("F", labResult.ResultStatus);
    }

    [Fact]
    public void ParsesOneOrderPerObr()
    {
        var result = new OruR01Parser().Parse(ReadFixture("oru-r01-two-orders.hl7"));

        Assert.Collection(
            result.Orders,
            first =>
            {
                Assert.Equal("OBR002", first.AccessionNo);
                Assert.Equal("11585-7", first.TestCode);
                Assert.Equal(new DateOnly(2026, 3, 13), first.CollectedOn);
                Assert.Equal("119364003", first.SpecimenCode);
                Assert.Single(first.Results);
            },
            second =>
            {
                Assert.Equal("OBR003", second.AccessionNo);
                Assert.Equal("5196-1", second.TestCode);
                Assert.Null(second.SpecimenCode);
                Assert.Collection(
                    second.Results,
                    coded => Assert.Equal("Not detected", coded.CodedResult),
                    numeric =>
                    {
                        Assert.Equal("4.1", numeric.ResultValue);
                        Assert.Null(numeric.CodedResult);
                        Assert.Equal("g/dL", numeric.Units);
                        Assert.Equal("3.5-5.0", numeric.ReferenceRange);
                    });
            });
    }

    [Fact]
    public void IsTheHl7V2MessageParser()
    {
        IMessageParser parser = new OruR01Parser();

        Assert.Equal(OruR01Parser.Hl7V2Format, parser.Format);
    }

    [Fact]
    public void RefusesAMalformedMessage()
    {
        var parser = new OruR01Parser();

        Assert.Throws<Hl7ParseException>(
            () => parser.Parse(ReadFixture("oru-r01-malformed.hl7")));
    }
}
