using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The parser read over fixtures derived from the ORU^R01 message documented
/// under ../nedss/docs/: the Chlamydia result it must land into the intake
/// records, and a malformed message it must refuse.
/// </summary>
public class OruR01ParserTests
{
    private static readonly string FixtureRoot =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Hl7");

    private static string ReadFixture(string name) =>
        File.ReadAllText(Path.Combine(FixtureRoot, name));

    [Fact]
    public void ParsesTheDocumentedChlamydiaResultIntoPersonAndLab()
    {
        var result = new OruR01Parser().Parse(ReadFixture("oru-r01-chlamydia.hl7"));

        Assert.Equal("DOE", result.Person.LastName);
        Assert.Equal("JOHN", result.Person.FirstName);
        Assert.Equal("A", result.Person.MiddleName);
        Assert.Equal(new DateOnly(1980, 1, 15), result.Person.BirthDate);
        Assert.Equal("M", result.Person.BirthGender);

        Assert.Equal(new DateOnly(2026, 3, 12), result.Lab.CollectionDate);

        var test = Assert.Single(result.Lab.Tests!);
        Assert.Equal("11585-7", test.LoincCode);
        Assert.Equal(new DateOnly(2026, 3, 12), test.LabTestDate);

        var labResult = Assert.Single(test.Results!);
        Assert.Equal("260373001", labResult.ResultValue);
        Assert.Equal("Detected", labResult.TestResult);
        Assert.True(labResult.PrimaryLabResult);
    }

    [Fact]
    public void RefusesAMalformedMessage()
    {
        var parser = new OruR01Parser();

        Assert.Throws<Hl7ParseException>(
            () => parser.Parse(ReadFixture("oru-r01-malformed.hl7")));
    }
}
