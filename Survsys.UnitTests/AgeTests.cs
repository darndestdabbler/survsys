using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The age the Demographic tab shows, proved on the days that decide it: the
/// day before a birthday, the day of one, a birth on a leap day, the days-old
/// fallback and a person nothing is known about.
/// </summary>
public class AgeTests
{
    private static readonly DateOnly Born = new(1985, 6, 15);

    [Fact]
    public void TheDayBeforeABirthdayIsStillTheOldAge()
    {
        Assert.Equal(40, Age.InYears(Born, null, new DateOnly(2026, 6, 14)));
    }

    [Fact]
    public void TheDayOfABirthdayIsTheNewAge()
    {
        Assert.Equal(41, Age.InYears(Born, null, new DateOnly(2026, 6, 15)));
    }

    [Fact]
    public void TheDayAfterABirthdayIsStillTheNewAge()
    {
        Assert.Equal(41, Age.InYears(Born, null, new DateOnly(2026, 6, 16)));
    }

    /// <summary>
    /// An earlier month in the year of the birthday is the old age however
    /// late in the month the day is: the year's difference alone would be one
    /// too many.
    /// </summary>
    [Fact]
    public void AnEarlierMonthInTheBirthdayYearIsTheOldAge()
    {
        Assert.Equal(40, Age.InYears(Born, null, new DateOnly(2026, 1, 31)));
    }

    [Fact]
    public void TheDayOfBirthIsAgeZero()
    {
        Assert.Equal(0, Age.InYears(Born, null, Born));
    }

    /// <summary>
    /// A birth on 29 February: in a year without one the birthday is reached
    /// on 28 February, and in a leap year it is reached on the day itself and
    /// not the day before.
    /// </summary>
    [Theory]
    [InlineData(2025, 2, 27, 20)]
    [InlineData(2025, 2, 28, 21)]
    [InlineData(2025, 3, 1, 21)]
    [InlineData(2028, 2, 28, 23)]
    [InlineData(2028, 2, 29, 24)]
    public void ALeapDayBirthHasItsBirthdayOnTheLastDayOfFebruary(
        int year, int month, int day, int expected)
    {
        var leapDay = new DateOnly(2004, 2, 29);

        Assert.Equal(expected, Age.InYears(leapDay, null, new DateOnly(year, month, day)));
    }

    /// <summary>
    /// The legacy fallback for an infant reported without a date of birth: a
    /// count of days, shown as the whole years it makes.
    /// </summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(14, 0)]
    [InlineData(364, 0)]
    [InlineData(365, 1)]
    [InlineData(800, 2)]
    public void WithNoBirthDateTheDaysOldAreTurnedIntoYears(int days, int expected)
    {
        Assert.Equal(expected, Age.InYears(null, days, new DateOnly(2026, 6, 15)));
    }

    /// <summary>
    /// The date of birth is the better fact and wins where both are known.
    /// </summary>
    [Fact]
    public void ADateOfBirthIsReadBeforeTheDaysOld()
    {
        Assert.Equal(41, Age.InYears(Born, 14, new DateOnly(2026, 6, 15)));
    }

    [Fact]
    public void WithNeitherThereIsNoAge()
    {
        Assert.Null(Age.InYears(null, null, new DateOnly(2026, 6, 15)));
    }

    /// <summary>
    /// A birth after the day asked about is not a negative age but no age:
    /// an onset before the birth, or a date typed wrong, shows nothing.
    /// </summary>
    [Fact]
    public void ABirthAfterTheDayAskedAboutIsNoAge()
    {
        Assert.Null(Age.InYears(Born, null, new DateOnly(1985, 6, 14)));
        Assert.Null(Age.InYears(null, -1, new DateOnly(2026, 6, 15)));
    }
}
