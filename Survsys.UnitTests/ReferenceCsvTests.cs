using Survsys.Api;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The CSV every reference table shares: a header that does not name the
/// columns refused, a row that is not one refused at its line, and a field
/// read as a number or a flag, or saying why it is not one.
/// </summary>
public class ReferenceCsvTests
{
    private static readonly IReadOnlyList<string> Columns = ["category", "concept", "app_value"];

    private static CsvRow Row(string column, string? value) =>
        new(2, new Dictionary<string, string?> { [column] = value });

    [Fact]
    public void WriteThenReadReturnsTheFieldsByColumn()
    {
        var (rows, refused) = ReferenceCsv.Read(
            ReferenceCsv.Write(Columns, [["specimen", "Swab, \"nasal\"", null]]), Columns);

        Assert.Empty(refused);
        var row = Assert.Single(rows);
        Assert.Equal(2, row.Line);
        Assert.Equal("Swab, \"nasal\"", row.Value("concept"));
        Assert.Null(row.Value("app_value"));
    }

    [Fact]
    public void ReadRefusesAFileWithNoHeader()
    {
        var (rows, refused) = ReferenceCsv.Read("  \r\n", Columns);

        Assert.Empty(rows);
        Assert.Equal(1, Assert.Single(refused).Line);
    }

    [Fact]
    public void ReadRefusesAHeaderMissingAColumn()
    {
        var (rows, refused) = ReferenceCsv.Read("category,concept\r\nspecimen,Blood\r\n", Columns);

        Assert.Empty(rows);
        Assert.Contains("app_value", Assert.Single(refused).Reason);
    }

    [Fact]
    public void ReadRefusesAHeaderNamingAColumnTwice()
    {
        var (rows, refused) = ReferenceCsv.Read(
            "category,Category,concept,app_value\r\nspecimen,ignored,Blood,Blood\r\n", Columns);

        Assert.Empty(rows);
        var problem = Assert.Single(refused);
        Assert.Equal(1, problem.Line);
        Assert.Contains("'category' more than once", problem.Reason);
    }

    [Fact]
    public void ReadRefusesARowWithTheWrongNumberOfFieldsAndKeepsTheRest()
    {
        var (rows, refused) = ReferenceCsv.Read(
            "Category,Concept,App_Value\nspecimen,Blood,Blood\n\nspecimen,Urine\nspecimen,\"Swab, nasal\",Swab\n",
            Columns);

        Assert.Equal(4, Assert.Single(refused).Line);
        Assert.Equal([2, 5], rows.Select(row => row.Line));
        Assert.Equal("Swab, nasal", rows[1].Value("concept"));
    }

    [Fact]
    public void ReadKeepsALineBreakInsideAQuotedFieldAndCountsLinesPastIt()
    {
        var (rows, refused) = ReferenceCsv.Read(
            ReferenceCsv.Write(Columns, [["note", "First line.\r\nSecond line.", "x"], ["specimen", "Blood", "y"]]),
            Columns);

        Assert.Empty(refused);
        Assert.Equal([2, 4], rows.Select(row => row.Line));
        Assert.Equal("First line.\r\nSecond line.", rows[0].Value("concept"));
        Assert.Equal("Blood", rows[1].Value("concept"));
    }

    [Fact]
    public void ReadTakesOffTheFormulaGuardWriteAdds()
    {
        var (rows, _) = ReferenceCsv.Read(ReferenceCsv.Write(Columns, [["-NEG", "=1", "+x"]]), Columns);

        var row = Assert.Single(rows);
        Assert.Equal(("-NEG", "=1", "+x"), (row.Value("category"), row.Value("concept"), row.Value("app_value")));
    }

    [Fact]
    public void OptionalReadsAWholeNumberOrABlank()
    {
        Assert.Null(ReferenceCsv.Optional(Row("n", "42"), "n", out var number));
        Assert.Equal(42, number);
        Assert.Null(ReferenceCsv.Optional(Row("n", null), "n", out var blank));
        Assert.Null(blank);
        Assert.Contains("not a whole number", ReferenceCsv.Optional(Row("n", "4.2"), "n", out _));
    }

    [Fact]
    public void RequiredRefusesABlank()
    {
        Assert.Null(ReferenceCsv.Required(Row("n", "7"), "n", out var number));
        Assert.Equal(7, number);
        Assert.Equal("n is required.", ReferenceCsv.Required(Row("n", null), "n", out _));
    }

    [Theory]
    [InlineData("yes", true)]
    [InlineData("TRUE", true)]
    [InlineData("1", true)]
    [InlineData("no", false)]
    [InlineData(null, false)]
    public void FlagReadsYesOrNo(string? text, bool expected)
    {
        Assert.Null(ReferenceCsv.Flag(Row("f", text), "f", out var value));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void FlagRefusesAnythingElse()
    {
        Assert.Contains("is not yes or no", ReferenceCsv.Flag(Row("f", "maybe"), "f", out _));
    }

    [Fact]
    public void NumberAndYesNoWriteTheirFields()
    {
        Assert.Equal("12", ReferenceCsv.Number(12));
        Assert.Null(ReferenceCsv.Number(null));
        Assert.Equal(("yes", "no"), (ReferenceCsv.YesNo(true), ReferenceCsv.YesNo(false)));
    }
}
