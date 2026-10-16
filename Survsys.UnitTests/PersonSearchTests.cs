using Survsys.Api;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The two things the search criteria say without a database: which criteria
/// were actually given, and what the digits of a telephone number are.
/// </summary>
/// <remarks>
/// What the criteria <em>match</em> is not here, and cannot honestly be: the
/// prefix and substring comparisons are made by SQL Server under the column's
/// collation, so whether <c>rodriguez</c> finds Rodriguez is a fact about the
/// database and is proved in <c>Survsys.IntegrationTests</c> against a real
/// one. This file proves the part that is a rule rather than a query.
/// </remarks>
public class PersonSearchTests
{
    [Fact]
    public void NoCriteriaAtAllIsEmpty()
    {
        Assert.True(new PersonSearch().IsEmpty);
    }

    /// <summary>
    /// What a browser submits for a form nobody typed in: every box present
    /// and every box blank. It is the same as having asked for nothing, and a
    /// screen that showed its empty form would otherwise search for people
    /// whose names begin with a space.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("   ")]
    public void CriteriaThatAreAllBlankAreEmpty(string blank)
    {
        var search = new PersonSearch(
            LastName: blank,
            FirstName: blank,
            MiddleName: blank,
            BirthDate: null,
            Phone: blank,
            BirthGender: blank);

        Assert.True(search.IsEmpty);
    }

    [Fact]
    public void ACriterionThatSaysSomethingIsNotEmpty()
    {
        Assert.False(new PersonSearch(LastName: "Rodriguez").IsEmpty);
        Assert.False(new PersonSearch(FirstName: "Maria").IsEmpty);
        Assert.False(new PersonSearch(MiddleName: "Elena").IsEmpty);
        Assert.False(new PersonSearch(BirthDate: new DateOnly(1985, 6, 15)).IsEmpty);
        Assert.False(new PersonSearch(Phone: "8015551234").IsEmpty);
        Assert.False(new PersonSearch(BirthGender: "F").IsEmpty);
    }

    /// <summary>
    /// A telephone criterion of nothing but punctuation says nothing, and the
    /// digits of it are null rather than empty — an empty substring would
    /// match every telephone there is.
    /// </summary>
    [Theory]
    [InlineData("()")]
    [InlineData("-")]
    [InlineData("( ) -")]
    public void APhoneOfPunctuationAloneIsEmpty(string punctuation)
    {
        var search = new PersonSearch(Phone: punctuation);

        Assert.False(search.IsEmpty);
        Assert.Null(search.PhoneDigits);
    }

    [Fact]
    public void TrimmedTurnsABlankCriterionIntoAnAbsentOne()
    {
        var trimmed = new PersonSearch(
            LastName: "",
            FirstName: "  ",
            MiddleName: "\t",
            BirthDate: null,
            Phone: " ",
            BirthGender: "").Trimmed();

        Assert.Null(trimmed.LastName);
        Assert.Null(trimmed.FirstName);
        Assert.Null(trimmed.MiddleName);
        Assert.Null(trimmed.Phone);
        Assert.Null(trimmed.BirthGender);
    }

    [Fact]
    public void TrimmedTakesTheSpacesOffACriterionAndLeavesTheRest()
    {
        var trimmed = new PersonSearch(
            LastName: "  Rodriguez ",
            FirstName: "Maria ",
            MiddleName: " Elena",
            BirthDate: new DateOnly(1985, 6, 15),
            Phone: " (801) 555-1234 ",
            BirthGender: " F ").Trimmed();

        Assert.Equal("Rodriguez", trimmed.LastName);
        Assert.Equal("Maria", trimmed.FirstName);
        Assert.Equal("Elena", trimmed.MiddleName);
        Assert.Equal(new DateOnly(1985, 6, 15), trimmed.BirthDate);
        Assert.Equal("(801) 555-1234", trimmed.Phone);
        Assert.Equal("F", trimmed.BirthGender);
    }

    /// <summary>
    /// A name with a space inside it keeps it: "Van Dyke" is one surname, and
    /// only the spaces a form puts around a value come off.
    /// </summary>
    [Fact]
    public void TrimmedKeepsTheSpacesInsideAName()
    {
        Assert.Equal("Van Dyke", new PersonSearch(LastName: " Van Dyke ").Trimmed().LastName);
    }

    /// <summary>
    /// The criteria are a record and nothing mutates: what a caller sent stays
    /// exactly as it was sent, and the trimmed criteria are a second value.
    /// </summary>
    [Fact]
    public void TrimmedLeavesTheCriteriaItWasAskedOfAlone()
    {
        var search = new PersonSearch(LastName: "  Rodriguez ");

        var trimmed = search.Trimmed();

        Assert.Equal("  Rodriguez ", search.LastName);
        Assert.Equal("Rodriguez", trimmed.LastName);
    }

    /// <summary>
    /// One number typed every way a person types one, against the two columns
    /// the legacy schema stores it in: area code <c>801</c> and number
    /// <c>5551234</c>.
    /// </summary>
    [Theory]
    [InlineData("(801) 555-1234")]
    [InlineData("801-555-1234")]
    [InlineData("801.555.1234")]
    [InlineData("801 555 1234")]
    [InlineData("8015551234")]
    [InlineData("+1 (801) 555-1234", "18015551234")]
    public void DigitsKeepsTheDigitsAndDropsTheRest(string typed, string expected = "8015551234")
    {
        Assert.Equal(expected, PersonSearch.Digits(typed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("()- ")]
    [InlineData("ext")]
    public void DigitsIsNullWhereThereAreNone(string? typed)
    {
        Assert.Null(PersonSearch.Digits(typed));
    }

    /// <summary>
    /// A digit of another script is not a digit this rule keeps: the stored
    /// numbers are ASCII, so keeping one could only produce a comparison that
    /// quietly matches nothing.
    /// </summary>
    [Fact]
    public void DigitsKeepsOnlyAsciiDigits()
    {
        Assert.Equal("801", PersonSearch.Digits("801٥٥٥"));
    }
}
