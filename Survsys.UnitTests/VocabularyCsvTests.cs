using Survsys.Api;
using Survsys.Contracts;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The vocabulary CSV: each table's rows written and read back unchanged, a
/// header that does not name the columns refused, a row that is not one
/// refused at its line, and the formula guard taken off on the way back.
/// </summary>
public class VocabularyCsvTests
{
    private static IReadOnlyList<CsvRow> RoundTrip(IReadOnlyList<string> columns, IReadOnlyList<string?> fields)
    {
        var (rows, refused) = VocabularyCsv.Read(VocabularyCsv.Write(columns, [fields]), columns);
        Assert.Empty(refused);
        return rows;
    }

    [Fact]
    public void WriteThenReadReturnsAMasterLoinc()
    {
        var row = new VocabMasterLoincRow(0, "11585-7", "Chlamydia, Ag", 42, 7, true);

        var read = VocabularyCsv.MasterLoinc(Assert.Single(RoundTrip(VocabularyCsv.MasterLoincColumns, VocabularyCsv.Fields(row))));

        Assert.Null(read.Why);
        Assert.Equal(row, read.Row);
    }

    [Fact]
    public void WriteThenReadReturnsAChildLoinc()
    {
        var row = new VocabChildLoincRow(0, 3, "43304-5", "11585-7", "CT \"Ag\"", false);

        var read = VocabularyCsv.ChildLoinc(Assert.Single(RoundTrip(VocabularyCsv.ChildLoincColumns, VocabularyCsv.Fields(row))));

        Assert.Equal(row, read.Row);
    }

    [Fact]
    public void WriteThenReadReturnsAnOrganism()
    {
        var row = new VocabMasterOrganismRow(0, "27268008", null, "Salmonella", null);

        var read = VocabularyCsv.Organism(Assert.Single(RoundTrip(VocabularyCsv.OrganismColumns, VocabularyCsv.Fields(row))));

        Assert.Equal(row, read.Row);
    }

    [Fact]
    public void WriteThenReadReturnsAMasterValue()
    {
        var row = new VocabMasterValueRow(0, "test_result", "Detected", "Positive");

        var read = VocabularyCsv.MasterValue(Assert.Single(RoundTrip(VocabularyCsv.MasterValueColumns, VocabularyCsv.Fields(row))));

        Assert.Equal(row, read.Row);
    }

    [Fact]
    public void WriteThenReadReturnsAChildValueWhoseCodeLooksLikeAFormula()
    {
        // The writer guards a leading '-' against spreadsheets; the reader
        // takes the guard off again.
        var row = new VocabChildValueRow(0, 3, "test_result", "-NEG", "Not detected");

        var read = VocabularyCsv.ChildValue(Assert.Single(RoundTrip(VocabularyCsv.ChildValueColumns, VocabularyCsv.Fields(row))));

        Assert.Equal(row, read.Row);
    }

    [Fact]
    public void ReadRefusesAFileWithNoHeader()
    {
        var (rows, refused) = VocabularyCsv.Read("  \r\n", VocabularyCsv.MasterValueColumns);

        Assert.Empty(rows);
        Assert.Equal(1, Assert.Single(refused).Line);
    }

    [Fact]
    public void ReadRefusesAHeaderMissingAColumn()
    {
        var (rows, refused) = VocabularyCsv.Read("category,concept\r\nspecimen,Blood\r\n", VocabularyCsv.MasterValueColumns);

        Assert.Empty(rows);
        Assert.Contains("app_value", Assert.Single(refused).Reason);
    }

    [Fact]
    public void ReadRefusesARowWithTheWrongNumberOfFieldsAndKeepsTheRest()
    {
        var (rows, refused) = VocabularyCsv.Read(
            "Category,Concept,App_Value\nspecimen,Blood,Blood\n\nspecimen,Urine\nspecimen,\"Swab, nasal\",Swab\n",
            VocabularyCsv.MasterValueColumns);

        Assert.Equal(4, Assert.Single(refused).Line);
        Assert.Equal([2, 5], rows.Select(row => row.Line));
        Assert.Equal("Swab, nasal", rows[1].Value("concept"));
    }

    [Theory]
    [InlineData("lab_id,child_loinc,master_loinc,concept_name,archived\nthree,X,Y,,no", "lab_id")]
    [InlineData("lab_id,child_loinc,master_loinc,concept_name,archived\n,X,Y,,no", "lab_id is required")]
    [InlineData("lab_id,child_loinc,master_loinc,concept_name,archived\n3,X,Y,,maybe", "archived")]
    public void AChildLoincRowThatIsNotOneSaysWhy(string csv, string expected)
    {
        var (rows, _) = VocabularyCsv.Read(csv, VocabularyCsv.ChildLoincColumns);

        var read = VocabularyCsv.ChildLoinc(Assert.Single(rows));

        Assert.Null(read.Row);
        Assert.Contains(expected, read.Why);
    }
}
