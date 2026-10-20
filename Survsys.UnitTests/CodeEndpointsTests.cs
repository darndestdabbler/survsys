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
    public void CsvColumnsAddTheCountysFieldsAndTheTestTypesConditions()
    {
        string[] common = ["code", "description", "sort_order", "active"];

        Assert.Equal(common, CodeEndpoints.CsvColumns(CodeLists.Race));
        Assert.Equal([.. common, "state", "fips", "kind"], CodeEndpoints.CsvColumns(CodeLists.County));
        Assert.Equal([.. common, "condition_ids"], CodeEndpoints.CsvColumns(CodeLists.TestType));
    }

    [Theory]
    [InlineData(CodeLists.County)]
    [InlineData(CodeLists.TestType)]
    [InlineData(CodeLists.SpecimenSource)]
    public void CsvFieldsReadBackAsTheSameCode(string list)
    {
        var county = list == CodeLists.County;
        var code = new CodeResponse(
            0, list, "09110", "Capitol", 5, false,
            list == CodeLists.TestType ? [4, 3] : [],
            county ? "CT" : null, county ? "09110" : null, county ? "county-equivalent" : null);
        var columns = CodeEndpoints.CsvColumns(list);

        var (rows, refused) = ReferenceCsv.Read(ReferenceCsv.Write(columns, [CodeEndpoints.CsvFields(list, code)]), columns);
        var (read, why) = CodeEndpoints.FromCsv(list, Assert.Single(rows));

        Assert.Empty(refused);
        Assert.Null(why);
        Assert.Equal(CodeEndpoints.CsvFields(list, code), CodeEndpoints.CsvFields(list, read!));
    }

    [Fact]
    public void FromCsvTakesABlankSortOrderAsZeroAndABlankActiveAsYes()
    {
        var (rows, _) = ReferenceCsv.Read("code,description,sort_order,active\r\nBD,,,\r\n", CodeEndpoints.CsvColumns(CodeLists.Race));

        var (read, _) = CodeEndpoints.FromCsv(CodeLists.Race, Assert.Single(rows));

        Assert.Equal(("BD", 0, true), (read!.Code, read.SortOrder, read.Active));
    }

    [Theory]
    [InlineData("code,description,sort_order,active,condition_ids\r\nCulture,,first,yes,", "sort_order")]
    [InlineData("code,description,sort_order,active,condition_ids\r\nCulture,,1,maybe,", "active")]
    [InlineData("code,description,sort_order,active,condition_ids\r\nCulture,,1,yes,3;four", "condition_ids 'four'")]
    public void FromCsvSaysWhyARowIsNotACode(string csv, string expected)
    {
        var (rows, _) = ReferenceCsv.Read(csv, CodeEndpoints.CsvColumns(CodeLists.TestType));

        var (read, why) = CodeEndpoints.FromCsv(CodeLists.TestType, Assert.Single(rows));

        Assert.Null(read);
        Assert.Contains(expected, why);
    }

    [Fact]
    public void WhyNotACsvCodeRefusesWhatWhyNotACodeRefusesAndACountyThatIsNotOne()
    {
        static CodeResponse County(string code, string? state = "CT", string? fips = "09110", string? kind = "county") =>
            new(0, CodeLists.County, code, null, 0, true, [], state, fips, kind);

        Assert.Null(CodeEndpoints.WhyNotACsvCode(CodeLists.County, County("09110")));
        Assert.Null(CodeEndpoints.WhyNotACsvCode(CodeLists.County, County("SL", state: null, fips: null, kind: null)));
        Assert.Contains("code is required", CodeEndpoints.WhyNotACsvCode(CodeLists.County, County("")));
        Assert.Contains("state is longer than 2", CodeEndpoints.WhyNotACsvCode(CodeLists.County, County("X", state: "CTX")));
        Assert.Contains("fips is longer than 5", CodeEndpoints.WhyNotACsvCode(CodeLists.County, County("X", fips: "091100")));
        Assert.Contains("kind 'region'", CodeEndpoints.WhyNotACsvCode(CodeLists.County, County("X", kind: "region")));
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
