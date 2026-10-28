using Survsys.Api;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The eCR data quality figures made from documents in memory: an RR counted
/// under its eICR's sender, an eICR under each condition its RRs keep, the
/// findings and latency summed, and the halves of a pair that arrived alone.
/// That the documents are read from the database with their conditions is the
/// integration suite's to prove.
/// </summary>
public class EcrQualityTests
{
    private const string Salem = "2.16.840.1.113883.4.6^88888888";

    private const string Hartford = "2.16.840.1.113883.4.6^77777777";

    private static readonly DateTime Received = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static EcrQualityDocument Eicr(
        int id,
        string documentId,
        string? sender,
        DateTime? created = null,
        int errors = 0,
        int warnings = 0,
        string feed = EcrDocument.ProductionFeed,
        bool counted = true) =>
        new(id, EcrDocument.EicrKind, feed, documentId, sender, Received, created, errors, warnings, null, [], counted);

    private static EcrQualityDocument Rr(
        int id,
        string? eicrDocumentId,
        string[] conditions,
        int errors = 0,
        string feed = EcrDocument.ProductionFeed,
        bool counted = true) =>
        new(id, EcrDocument.RrKind, feed, $"rr-{id}", "APHL", Received, null, errors, 0, eicrDocumentId, conditions, counted);

    [Fact]
    public void RowsCountAnRrUnderItsEicrsSenderAndConditions()
    {
        var row = Assert.Single(EcrQuality.Rows([Eicr(1, "e1", Salem), Rr(2, "e1", ["Pertussis"])]));

        Assert.Equal(Salem, row.Sender);
        Assert.Null(row.SenderName);
        Assert.Equal("Pertussis", row.Condition);
        Assert.Equal(1, row.Eicrs);
        Assert.Equal(1, row.Rrs);
        Assert.Equal(0, row.EicrsWithoutRr);
        Assert.Equal(0, row.RrsWithoutEicr);
    }

    [Fact]
    public void RowsCountAnEicrOnceUnderEachConditionItsRrsKeep()
    {
        var rows = EcrQuality.Rows([Eicr(1, "e1", Salem, errors: 2), Rr(2, "e1", ["Pertussis", "Measles"])]);

        Assert.Equal(["Measles", "Pertussis"], rows.Select(row => row.Condition));
        Assert.All(rows, row =>
        {
            Assert.Equal(1, row.Eicrs);
            Assert.Equal(1, row.Rrs);
            Assert.Equal(2, row.Errors);
        });
    }

    [Fact]
    public void RowsCountAnEicrWithNoRrUnderNoCondition()
    {
        var row = Assert.Single(EcrQuality.Rows([Eicr(1, "e1", Salem)]));

        Assert.Equal(Salem, row.Sender);
        Assert.Null(row.Condition);
        Assert.Equal(1, row.EicrsWithoutRr);
    }

    [Fact]
    public void RowsCountAnRrWithNoEicrUnderNoSender()
    {
        var row = Assert.Single(EcrQuality.Rows([Rr(1, "missing", ["Pertussis"])]));

        Assert.Null(row.Sender);
        Assert.Equal("Pertussis", row.Condition);
        Assert.Equal(1, row.Rrs);
        Assert.Equal(1, row.RrsWithoutEicr);
    }

    [Fact]
    public void RowsPairOnlyWithinAFeed()
    {
        var rows = EcrQuality.Rows([Eicr(1, "e1", Salem, feed: EcrDocument.TestFeed), Rr(2, "e1", ["Pertussis"])]);

        Assert.Equal(1, Assert.Single(rows, row => row.Sender == Salem).EicrsWithoutRr);
        Assert.Equal(1, Assert.Single(rows, row => row.Sender is null).RrsWithoutEicr);
    }

    [Fact]
    public void RowsPairWithADocumentNotCountedButCountOnlyTheCounted()
    {
        var row = Assert.Single(EcrQuality.Rows([Eicr(1, "e1", Salem, counted: false), Rr(2, "e1", ["Pertussis"])]));

        Assert.Equal(Salem, row.Sender);
        Assert.Equal(0, row.Eicrs);
        Assert.Equal(1, row.Rrs);
        Assert.Equal(0, row.RrsWithoutEicr);
    }

    [Fact]
    public void RowsSumFindingsAndAverageTheLatencyOfEicrsThatSayWhenTheyWereCreated()
    {
        var row = Assert.Single(EcrQuality.Rows(
        [
            Eicr(1, "e1", Salem, created: Received.AddMinutes(-30), errors: 1, warnings: 2),
            Eicr(2, "e2", Salem, created: Received.AddMinutes(-90), warnings: 1),
            Eicr(3, "e3", Salem),
        ]));

        Assert.Equal(3, row.Eicrs);
        Assert.Equal(1, row.Errors);
        Assert.Equal(3, row.Warnings);
        Assert.Equal(60, row.AverageLatencyMinutes);
        Assert.Equal(90, row.LongestLatencyMinutes);
        Assert.Equal(3, row.EicrsWithoutRr);
    }

    [Fact]
    public void RowsHaveNoLatencyWhereNoEicrSaysWhenItWasCreated()
    {
        var row = Assert.Single(EcrQuality.Rows([Eicr(1, "e1", Salem)]));

        Assert.Null(row.AverageLatencyMinutes);
        Assert.Null(row.LongestLatencyMinutes);
    }

    [Fact]
    public void RowsAreBySenderThenConditionWithNoneLast()
    {
        var rows = EcrQuality.Rows(
        [
            Rr(1, "missing", ["Pertussis"]),
            Eicr(2, "e2", Salem),
            Eicr(3, "e3", Hartford),
            Rr(4, "e3", ["Pertussis"]),
            Eicr(5, "e5", Hartford),
        ]);

        Assert.Equal(
            new (string?, string?)[] { (Hartford, "Pertussis"), (Hartford, null), (Salem, null), (null, "Pertussis") },
            rows.Select(row => (row.Sender, row.Condition)));
    }

    [Fact]
    public void RowsOfNothingAreNone()
    {
        Assert.Empty(EcrQuality.Rows([]));
    }
}
