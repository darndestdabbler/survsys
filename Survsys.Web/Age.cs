namespace Survsys.Web;

/// <summary>
/// A person's age in whole years, as the Demographic tab shows it: the
/// current age, and the age at the event's onset.
/// </summary>
/// <remarks>
/// A pure rule class, in the shape <c>Paging</c> and <c>RecordNumber</c> have
/// on the other tier and for the same reason: it takes no clock and no
/// component, so the unit suite proves it with dates and nothing else. The
/// screen supplies the day it is asking about — today for the current age,
/// the onset date for the age at onset — and this answers for that day.
///
/// None of the ages is stored. <c>Person.DaysOldNoBirthday</c> says so of the
/// schema: the legacy fallback for an infant reported without a date of birth
/// is a count of days, and the years a screen shows are computed from it or
/// from the date of birth, never written back.
/// </remarks>
public static class Age
{
    /// <summary>
    /// The days in a year, for turning the days-old fallback into years. The
    /// fallback is for infants, who are months old rather than years, so the
    /// quarter-day of a leap year does not change what it answers.
    /// </summary>
    private const int DaysInAYear = 365;

    /// <summary>
    /// The whole years between the date of birth and the day asked about, or
    /// the days-old fallback divided into years when there is no date of
    /// birth, or null when there is neither.
    /// </summary>
    /// <remarks>
    /// A birthday not yet reached in the year asked about counts one year
    /// less: the day before a birthday is the old age, and the day of it is
    /// the new one. A birth on 29 February has its birthday on 28 February in
    /// a year without one, which is what <see cref="DateOnly.AddYears"/> does
    /// with it and what this reads off that method rather than deciding for
    /// itself.
    ///
    /// A date of birth after the day asked about — a date typed wrong, or an
    /// onset before the birth — is null rather than a negative age: an age
    /// below zero is not an age, and a screen shows nothing for it rather
    /// than a minus sign.
    /// </remarks>
    /// <param name="birthDate">The person's date of birth, where it is known.</param>
    /// <param name="daysOldNoBirthday">
    /// The person's age in days, for a person whose birthday is not known;
    /// read only when <paramref name="birthDate"/> is null.
    /// </param>
    /// <param name="on">The day the age is asked for.</param>
    public static int? InYears(DateOnly? birthDate, int? daysOldNoBirthday, DateOnly on)
    {
        if (birthDate is { } born)
        {
            var years = on.Year - born.Year;

            if (on < born.AddYears(years))
            {
                years--;
            }

            return years < 0 ? null : years;
        }

        if (daysOldNoBirthday is { } days)
        {
            return days < 0 ? null : days / DaysInAYear;
        }

        return null;
    }
}
