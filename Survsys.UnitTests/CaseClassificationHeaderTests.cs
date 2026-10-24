using System.Text.Json;
using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The value of the header a route that classified a case answers with.
/// </summary>
public class CaseClassificationHeaderTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ValueCarriesTheCaseItsStatusAndItsFindings()
    {
        var value = CaseClassificationHeader.Value(
            7, new ClassificationOutcome("C", 3, false, ["Chlamydia: a positive result.", "Second line."]));

        var read = JsonSerializer.Deserialize<CaseClassificationResponse>(value, Json)!;
        Assert.Equal(7, read.ReportId);
        Assert.Equal("C", read.CaseStatus);
        Assert.Equal(["Chlamydia: a positive result.", "Second line."], read.Findings);
    }

    [Fact]
    public void ValueCarriesNoStatusAndNoFindings()
    {
        var read = JsonSerializer.Deserialize<CaseClassificationResponse>(
            CaseClassificationHeader.Value(7, new ClassificationOutcome(null, null, false, [])), Json)!;

        Assert.Null(read.CaseStatus);
        Assert.Empty(read.Findings);
    }

    [Fact]
    public void ValueIsPrintableAsciiWhateverTheFindingsSay()
    {
        var value = CaseClassificationHeader.Value(
            7, new ClassificationOutcome(null, null, false, ["Café\nsecond line"]));

        Assert.All(value, character => Assert.InRange(character, ' ', '~'));
        Assert.Equal(
            "Café\nsecond line",
            Assert.Single(JsonSerializer.Deserialize<CaseClassificationResponse>(value, Json)!.Findings));
    }
}
