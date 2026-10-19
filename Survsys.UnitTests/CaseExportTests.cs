using Survsys.Api;
using Survsys.Contracts;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// How a saved view's reports are written as CSV.
/// </summary>
public class CaseExportTests
{
    private static readonly CaseExportRow Row = new(
        "2026-0001",
        "Rivera",
        "Ana",
        new DateOnly(1990, 4, 2),
        "Salmonellosis",
        "Central District",
        "accepted",
        "C",
        new DateOnly(2026, 10, 1),
        null,
        40,
        2026,
        new DateTimeOffset(2026, 10, 3, 9, 30, 0, TimeSpan.Zero));

    [Fact]
    public void ValuesReadsTheChosenColumnsInTheirOrder()
    {
        Assert.Equal(
            ["Salmonellosis", "2026-0001", "2026-10-01", "40"],
            CaseExport.Values(Row, [ExportColumns.Condition, ExportColumns.RecordNumber, ExportColumns.EventOnsetDate, ExportColumns.MmwrWeek]));
    }

    [Fact]
    public void ValuesReadsEveryColumnInTheCatalogue()
    {
        Assert.Equal(
            ["2026-0001", "Rivera", "Ana", "1990-04-02", "Salmonellosis", "Central District", "accepted",
             "C", "2026-10-01", null, "40", "2026", "2026-10-03T09:30:00.0000000+00:00"],
            CaseExport.Values(Row, ExportColumns.All));
    }

    [Fact]
    public void ValuesRefusesAColumnOutsideTheCatalogue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CaseExport.Values(Row, ["ssn"]));
    }

    [Fact]
    public void CsvWritesTheHeaderThenOneLinePerRow()
    {
        Assert.Equal(
            "record_number,last_name\r\n2026-0001,Rivera\r\n2026-0002,Chen\r\n",
            CaseExport.Csv([ExportColumns.RecordNumber, ExportColumns.LastName], [["2026-0001", "Rivera"], ["2026-0002", "Chen"]]));
    }

    [Fact]
    public void CsvWritesOnlyTheHeaderWhenNoReportIsSelected()
    {
        Assert.Equal("record_number\r\n", CaseExport.Csv([ExportColumns.RecordNumber], []));
    }

    [Fact]
    public void CsvWritesAnEmptyValueAsAnEmptyField()
    {
        Assert.Equal("a,b\r\n,x\r\n", CaseExport.Csv(["a", "b"], [[null, "x"]]));
    }

    [Theory]
    [InlineData("Rivera, Jr.", "\"Rivera, Jr.\"")]
    [InlineData("O\"Neil", "\"O\"\"Neil\"")]
    [InlineData("two\r\nlines", "\"two\r\nlines\"")]
    [InlineData("line\nbreak", "\"line\nbreak\"")]
    public void CsvQuotesAValueHoldingACommaAQuoteOrALineBreak(string value, string field)
    {
        Assert.Equal($"a\r\n{field}\r\n", CaseExport.Csv(["a"], [[value]]));
    }

    [Theory]
    [InlineData("=SUM(A1)", "'=SUM(A1)")]
    [InlineData("+1", "'+1")]
    [InlineData("-1", "'-1")]
    [InlineData("@cmd", "'@cmd")]
    public void CsvWritesAValueASpreadsheetWouldReadAsAFormulaWithALeadingApostrophe(string value, string field)
    {
        Assert.Equal($"a\r\n{field}\r\n", CaseExport.Csv(["a"], [[value]]));
    }
}
