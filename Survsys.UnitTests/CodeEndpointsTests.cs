using Survsys.Api;
using Survsys.Contracts;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the code endpoints decide without the database: what a code to save
/// on a list must be.
/// </summary>
public class CodeEndpointsTests
{
    [Fact]
    public void WhyNotACodeAcceptsACodeWithADescription()
    {
        Assert.Null(CodeEndpoints.WhyNotACode(
            CodeLists.SpecimenSource,
            new SaveCodeRequest("BD", "Blood", SortOrder: 15)));
    }

    [Fact]
    public void WhyNotACodeAcceptsATestTypeWithConditions()
    {
        Assert.Null(CodeEndpoints.WhyNotACode(
            CodeLists.TestType,
            new SaveCodeRequest("Culture", ConditionIds: [3, 4])));
    }

    [Theory]
    [InlineData("specimen_sources")]
    [InlineData("Specimen_Source")]
    [InlineData("")]
    public void WhyNotACodeRefusesAListThatIsNotOne(string list)
    {
        Assert.Contains("is not a code list", CodeEndpoints.WhyNotACode(list, new SaveCodeRequest("BD")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WhyNotACodeRefusesABlankCode(string? code)
    {
        Assert.Contains("code is required", CodeEndpoints.WhyNotACode(CodeLists.Race, new SaveCodeRequest(code)));
    }

    [Fact]
    public void WhyNotACodeRefusesConditionsOnAnyListButTestTypes()
    {
        Assert.Contains(
            "only kept for the test_type list",
            CodeEndpoints.WhyNotACode(CodeLists.Organism, new SaveCodeRequest("Salmonella", ConditionIds: [3])));
    }

    [Fact]
    public void WhyNotACodeAcceptsAnEmptyConditionListOnAnyList()
    {
        Assert.Null(CodeEndpoints.WhyNotACode(CodeLists.Organism, new SaveCodeRequest("Salmonella", ConditionIds: [])));
    }

    [Fact]
    public void WhyNotACodeRefusesACodeOverOneHundredCharacters()
    {
        Assert.Null(CodeEndpoints.WhyNotACode(CodeLists.Organism, new SaveCodeRequest(new string('x', 100))));
        Assert.Contains(
            "code is longer than 100",
            CodeEndpoints.WhyNotACode(CodeLists.Organism, new SaveCodeRequest(new string('x', 101))));
    }

    [Fact]
    public void WhyNotACodeRefusesADescriptionOverFourHundredCharacters()
    {
        Assert.Null(CodeEndpoints.WhyNotACode(CodeLists.County, new SaveCodeRequest("SL", new string('x', 400))));
        Assert.Contains(
            "description is longer than 400",
            CodeEndpoints.WhyNotACode(CodeLists.County, new SaveCodeRequest("SL", new string('x', 401))));
    }
}
