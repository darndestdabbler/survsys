using System.Globalization;

namespace Survsys.Api;

/// <summary>
/// The record number a case morbidity report is known by: the legacy
/// <c>YYYYNNNNN</c> format, and the rule for what the next one is.
/// </summary>
/// <remarks>
/// The format is EpiTrax's own, as
/// <c>../nedss/docs/db-snapshots/session-010/02-after-cmr-creation.txt</c>
/// dumps it on <c>person_condition.record_number</c>: the four-digit year
/// followed by the sequence within that year, zero-padded to five digits, so
/// the first report of 2026 is <c>202600001</c>. Nine characters, all of them
/// digits, and the width is fixed — which is the property the rest of this
/// class rests on and the reason the padding is not cosmetic: because every
/// record number of a year is the same width, the largest of them as text is
/// also the largest of them as a number, so <c>MAX(record_number)</c> over the
/// year's rows is the highest sequence issued and no arithmetic is needed in
/// the database to find it.
///
/// Pure, and public because the unit suite proves it without starting a host —
/// the shape <see cref="Paging"/> and <see cref="MigrationGate"/> already have
/// in this tier, and here for the same reason. It holds no clock: which year a
/// report is created in is the caller's to decide, and the caller is the
/// endpoint that creates it. It holds no query either: reading the highest
/// record number already issued needs a database, so that stays at the call
/// site and only the decision is here.
///
/// There is no sequence table behind this and no retry in front of it. Two
/// reports created in the same instant can read the same highest number and
/// ask for the same next one; the unique index on <c>record_number</c> refuses
/// the second, which surfaces as a failure the caller sees rather than as two
/// reports sharing a number.
/// </remarks>
public static class RecordNumber
{
    /// <summary>How many characters a record number has: four of year, five of sequence.</summary>
    public const int Length = 9;

    /// <summary>The sequence the first report of a year takes.</summary>
    public const int FirstSequence = 1;

    /// <summary>
    /// The last sequence a year can hold. Five digits is the legacy format,
    /// and a year that has issued this many has no next record number — see
    /// <see cref="Next"/>, which refuses rather than widening the format.
    /// </summary>
    public const int LastSequence = 99_999;

    /// <summary>The first year the format can express, and the last.</summary>
    private const int FirstYear = 1000;

    private const int LastYear = 9999;

    /// <summary>How many of the nine characters are the year.</summary>
    private const int YearLength = 4;

    /// <summary>
    /// The record number for a year and a sequence within it.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The year is not one four digits can express, or the sequence is not one
    /// five digits can express. Either is a caller's mistake rather than a
    /// state the data can be in, and a record number of the wrong width would
    /// break the ordering the rest of this class rests on.
    /// </exception>
    public static string For(int year, int sequence)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, FirstYear);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, LastYear);
        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, FirstSequence);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sequence, LastSequence);

        return string.Create(
            CultureInfo.InvariantCulture, $"{year:D4}{sequence:D5}");
    }

    /// <summary>
    /// The next record number for a year, given the highest one already issued
    /// in it — or null where none has been, which is how a year's first report
    /// is asked for.
    /// </summary>
    /// <remarks>
    /// The year is passed separately rather than read out of
    /// <paramref name="highestIssued"/>, because the year a report belongs to
    /// is the year it is created in and not the year of whatever came before
    /// it: on the first of January the highest number issued is last year's,
    /// and the next one is <c>00001</c> of the new year. That is why a value
    /// carrying another year is refused here rather than followed — a caller
    /// that passed one asked a question about one year with the answer to
    /// another, and the query that produced it filtered by the wrong prefix.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The year is not one four digits can express.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="highestIssued"/> is not a record number of that year —
    /// see <see cref="SequenceIn"/> for what that means — or the year has
    /// issued <see cref="LastSequence"/> already. The second is the end of
    /// what the legacy format can express, and this rule will not roll into
    /// the next year or widen the format to get past it: a year that full is a
    /// thing for a person to decide about.
    /// </exception>
    public static string Next(int year, string? highestIssued)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, FirstYear);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, LastYear);

        if (string.IsNullOrEmpty(highestIssued))
        {
            return For(year, FirstSequence);
        }

        if (SequenceIn(highestIssued, year) is not { } highest)
        {
            throw new InvalidOperationException(
                $"'{highestIssued}' is not a record number of {year}, so the next record "
                + $"number of {year} cannot be read from it. A record number is {Length} "
                + $"digits: the four-digit year — {year} here — and the sequence within it "
                + "padded to five. Whatever produced this value selected the highest record "
                + "number of some other year, or a row holding something that is not a "
                + "record number at all.");
        }

        if (highest >= LastSequence)
        {
            throw new InvalidOperationException(
                $"{year} has issued all {LastSequence} record numbers the format allows, "
                + $"the last of them {highestIssued}. The legacy YYYYNNNNN format has five "
                + "digits of sequence and this rule will not roll into the next year or "
                + "widen the format: what happens next is a decision for a person and not "
                + "for a case being created.");
        }

        return For(year, highest + 1);
    }

    /// <summary>
    /// The sequence a record number carries, or null where the value is not a
    /// record number of the year asked about.
    /// </summary>
    /// <remarks>
    /// Null rather than an exception, because "is this a record number of
    /// 2026?" is a question this rule can answer about any string a caller
    /// hands it. The callers that cannot carry on without an answer say so
    /// themselves: <see cref="Next"/> is one, and it throws.
    ///
    /// Four things make a value unreadable, and all four are refused rather
    /// than repaired: it is not <see cref="Length"/> characters; it is not
    /// digits throughout, a leading sign, a space or a plus among them, since
    /// nothing but digits is a record number; its first four digits are not
    /// <paramref name="year"/>; or its sequence is <c>00000</c>, which no
    /// report was ever issued because the first of a year is
    /// <see cref="FirstSequence"/>.
    /// </remarks>
    public static int? SequenceIn(string? recordNumber, int year)
    {
        if (recordNumber is null || recordNumber.Length != Length)
        {
            return null;
        }

        foreach (var character in recordNumber)
        {
            if (character is < '0' or > '9')
            {
                return null;
            }
        }

        var carried = int.Parse(
            recordNumber.AsSpan(0, YearLength), provider: CultureInfo.InvariantCulture);

        if (carried != year)
        {
            return null;
        }

        var sequence = int.Parse(
            recordNumber.AsSpan(YearLength), provider: CultureInfo.InvariantCulture);

        return sequence < FirstSequence ? null : sequence;
    }
}
