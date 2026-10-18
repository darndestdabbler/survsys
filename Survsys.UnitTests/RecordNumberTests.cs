using Survsys.Api;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The legacy record number: what one looks like, what the next one is, and
/// what this rule refuses to read.
/// </summary>
/// <remarks>
/// Pure throughout, so every case here is the rule itself and not a database
/// or a host. The one real value in the file is <c>202600001</c>, which
/// <c>../nedss/docs/db-snapshots/session-010/02-after-cmr-creation.txt</c>
/// dumps as the record number of the first case morbidity report created in
/// that legacy environment; the rest are the boundaries it implies.
/// </remarks>
public class RecordNumberTests
{
    /// <summary>
    /// The record number the legacy snapshot carries, which is the first
    /// report of its year — so the format and the starting sequence are both
    /// asserted against a real value rather than against a reading of the
    /// format.
    /// </summary>
    private const string FirstOf2026 = "202600001";

    [Fact]
    public void ForPadsTheSequenceToFiveDigitsAfterTheYear()
    {
        Assert.Equal(FirstOf2026, RecordNumber.For(2026, RecordNumber.FirstSequence));
    }

    [Theory]
    [InlineData(2026, 1, "202600001")]
    [InlineData(2026, 9, "202600009")]
    [InlineData(2026, 10, "202600010")]
    [InlineData(2026, 42, "202600042")]
    [InlineData(2026, 1234, "202601234")]
    [InlineData(2026, RecordNumber.LastSequence, "202699999")]
    [InlineData(1999, 1, "199900001")]
    [InlineData(2100, 500, "210000500")]
    public void ForIsTheYearThenTheSequenceInNineDigits(int year, int sequence, string expected)
    {
        var number = RecordNumber.For(year, sequence);

        Assert.Equal(expected, number);
        Assert.Equal(RecordNumber.Length, number.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(RecordNumber.LastSequence + 1)]
    [InlineData(int.MaxValue)]
    public void ForRefusesASequenceFiveDigitsCannotExpress(int sequence)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RecordNumber.For(2026, sequence));
    }

    [Theory]
    [InlineData(999)]
    [InlineData(0)]
    [InlineData(-2026)]
    [InlineData(10_000)]
    public void ForRefusesAYearFourDigitsCannotExpress(int year)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RecordNumber.For(year, 1));
    }

    /// <summary>
    /// A year that has issued nothing. Null is what the query returns when no
    /// report of that year exists, and it is how the first report of a year is
    /// asked for.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NextIsTheFirstOfTheYearWhenNoneHasBeenIssued(string? highestIssued)
    {
        Assert.Equal(FirstOf2026, RecordNumber.Next(2026, highestIssued));
    }

    [Theory]
    [InlineData("202600001", "202600002")]
    [InlineData("202600009", "202600010")]
    [InlineData("202600099", "202600100")]
    [InlineData("202609999", "202610000")]
    [InlineData("202699998", "202699999")]
    public void NextFollowsTheHighestAlreadyIssued(string highestIssued, string expected)
    {
        Assert.Equal(expected, RecordNumber.Next(2026, highestIssued));
    }

    /// <summary>
    /// The turn of the year, which is the reason the year is a parameter and
    /// not read out of the value: the highest number issued is last year's,
    /// and the next one is the first of this year rather than the one after it.
    /// </summary>
    [Fact]
    public void NextStartsTheNewYearRatherThanFollowingLastYearsHighest()
    {
        Assert.Equal("202700001", RecordNumber.Next(2027, null));
    }

    [Fact]
    public void NextRefusesAYearThatHasIssuedEveryNumberTheFormatAllows()
    {
        var exhausted = RecordNumber.For(2026, RecordNumber.LastSequence);

        var refused = Assert.Throws<InvalidOperationException>(
            () => RecordNumber.Next(2026, exhausted));

        Assert.Contains(exhausted, refused.Message, StringComparison.Ordinal);
        Assert.Contains("will not roll into the next year", refused.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Each shape the reader refuses, through the caller that cannot carry on
    /// without an answer. The message names the value, because the row holding
    /// it is what someone will have to go and look at.
    /// </summary>
    [Theory]
    [InlineData("2026001")]
    [InlineData("2026000012")]
    [InlineData("20260000A")]
    [InlineData("2026 0001")]
    [InlineData("+20260001")]
    [InlineData("-20260001")]
    [InlineData("CMR-00001")]
    [InlineData("202500001")]
    [InlineData("202600000")]
    public void NextRefusesAHighestThatIsNotARecordNumberOfTheYear(string highestIssued)
    {
        var refused = Assert.Throws<InvalidOperationException>(
            () => RecordNumber.Next(2026, highestIssued));

        Assert.Contains(highestIssued, refused.Message, StringComparison.Ordinal);
        Assert.Contains("is not a record number of 2026", refused.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(999)]
    [InlineData(10_000)]
    public void NextRefusesAYearFourDigitsCannotExpress(int year)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RecordNumber.Next(year, null));
    }

    [Theory]
    [InlineData("202600001", 1)]
    [InlineData("202600042", 42)]
    [InlineData("202699999", RecordNumber.LastSequence)]
    public void SequenceInReadsTheSequenceBackOut(string recordNumber, int expected)
    {
        Assert.Equal(expected, RecordNumber.SequenceIn(recordNumber, 2026));
    }

    /// <summary>
    /// What the reader will not read, asked of it directly. Null and not an
    /// exception: whether a string is a record number of a given year is a
    /// question this rule answers about any string.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2026001")]
    [InlineData("2026000012")]
    [InlineData("20260000A")]
    [InlineData("2026 0001")]
    [InlineData("+20260001")]
    [InlineData("202500001")]
    [InlineData("202600000")]
    public void SequenceInIsNullForAnythingThatIsNotARecordNumberOfTheYear(string? recordNumber)
    {
        Assert.Null(RecordNumber.SequenceIn(recordNumber, 2026));
    }

    /// <summary>
    /// The property every record number of a year being the same width buys,
    /// and the one the creation endpoint's query depends on: the highest record
    /// number of a year as text is the highest sequence issued in it, so
    /// <c>MAX(record_number)</c> needs no arithmetic in the database.
    /// </summary>
    [Fact]
    public void TextOrderIsSequenceOrderWithinAYear()
    {
        var issued = Enumerable
            .Range(RecordNumber.FirstSequence, 250)
            .Concat([1_000, 9_999, 10_000, RecordNumber.LastSequence])
            .Select(sequence => RecordNumber.For(2026, sequence))
            .ToList();

        Assert.Equal(
            issued.OrderBy(number => number, StringComparer.Ordinal),
            issued.OrderBy(number => RecordNumber.SequenceIn(number, 2026)));
    }
}
