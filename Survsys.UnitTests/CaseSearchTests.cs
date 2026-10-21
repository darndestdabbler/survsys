using Survsys.Api;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The one thing the case search criteria say without a database: which
/// criteria were actually given.
/// </summary>
/// <remarks>
/// What the criteria <em>match</em> is not here, and cannot honestly be: the
/// prefix comparisons are made by SQL Server under the column's collation and
/// the date range against stored onsets, so whether <c>rod</c> finds Rodriguez
/// or a range finds a case on its last day is a fact about the database and is
/// proved in <c>Survsys.IntegrationTests</c> against a real one. This file
/// proves the part that is a rule rather than a query, as
/// <see cref="PersonSearchTests"/> does for the people search.
/// </remarks>
public class CaseSearchTests
{
    [Fact]
    public void NoCriteriaAtAllIsEmpty()
    {
        Assert.True(new CaseSearch().IsEmpty);
    }

    /// <summary>
    /// What a browser submits for a form nobody typed in: every text box
    /// present and every text box blank. It is the same as having asked for
    /// nothing, and a screen that showed its empty form would otherwise search
    /// for record numbers that begin with a space.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("   ")]
    public void CriteriaThatAreAllBlankAreEmpty(string blank)
    {
        var search = new CaseSearch(
            RecordNumber: blank,
            LastName: blank,
            FirstName: blank,
            ConditionId: null,
            AgencyId: null,
            PersonId: null,
            WorkflowState: blank,
            CaseStatus: blank,
            OnsetFrom: null,
            OnsetTo: null);

        Assert.True(search.IsEmpty);
    }

    [Fact]
    public void ACriterionThatSaysSomethingIsNotEmpty()
    {
        Assert.False(new CaseSearch(RecordNumber: "2026").IsEmpty);
        Assert.False(new CaseSearch(LastName: "Rodriguez").IsEmpty);
        Assert.False(new CaseSearch(FirstName: "Maria").IsEmpty);
        Assert.False(new CaseSearch(ConditionId: 1).IsEmpty);
        Assert.False(new CaseSearch(AgencyId: 1).IsEmpty);
        Assert.False(new CaseSearch(PersonId: 1).IsEmpty);
        Assert.False(new CaseSearch(WorkflowState: "accepted").IsEmpty);
        Assert.False(new CaseSearch(CaseStatus: "C").IsEmpty);
        Assert.False(new CaseSearch(OnsetFrom: new DateOnly(2026, 3, 1)).IsEmpty);
        Assert.False(new CaseSearch(OnsetTo: new DateOnly(2026, 3, 31)).IsEmpty);
    }

    [Fact]
    public void TrimmedTurnsABlankCriterionIntoAnAbsentOne()
    {
        var trimmed = new CaseSearch(
            RecordNumber: "",
            LastName: "  ",
            FirstName: "\t",
            WorkflowState: " ",
            CaseStatus: "").Trimmed();

        Assert.Null(trimmed.RecordNumber);
        Assert.Null(trimmed.LastName);
        Assert.Null(trimmed.FirstName);
        Assert.Null(trimmed.WorkflowState);
        Assert.Null(trimmed.CaseStatus);
    }

    /// <summary>
    /// The text criteria lose the spaces a form puts around them; the typed
    /// ones — the three ids and the two dates — have nothing to trim and pass
    /// through exactly as they were bound.
    /// </summary>
    [Fact]
    public void TrimmedTakesTheSpacesOffACriterionAndLeavesTheRest()
    {
        var trimmed = new CaseSearch(
            RecordNumber: " 202600004 ",
            LastName: "  Rodriguez ",
            FirstName: "Maria ",
            ConditionId: 3,
            AgencyId: 5,
            PersonId: 7,
            WorkflowState: " under_investigation",
            CaseStatus: " C ",
            OnsetFrom: new DateOnly(2026, 3, 1),
            OnsetTo: new DateOnly(2026, 3, 31)).Trimmed();

        Assert.Equal("202600004", trimmed.RecordNumber);
        Assert.Equal("Rodriguez", trimmed.LastName);
        Assert.Equal("Maria", trimmed.FirstName);
        Assert.Equal(3, trimmed.ConditionId);
        Assert.Equal(5, trimmed.AgencyId);
        Assert.Equal(7, trimmed.PersonId);
        Assert.Equal("under_investigation", trimmed.WorkflowState);
        Assert.Equal("C", trimmed.CaseStatus);
        Assert.Equal(new DateOnly(2026, 3, 1), trimmed.OnsetFrom);
        Assert.Equal(new DateOnly(2026, 3, 31), trimmed.OnsetTo);
    }

    /// <summary>
    /// Showing deleted reports is a switch on what is shown and not a
    /// criterion: it leaves the search empty, and trimming keeps it.
    /// </summary>
    [Fact]
    public void ShowDeletedIsNotACriterionAndTrimmedKeepsIt()
    {
        var search = new CaseSearch(ShowDeleted: true);

        Assert.True(search.IsEmpty);
        Assert.True(search.Trimmed().ShowDeleted);
        Assert.False(new CaseSearch().ShowDeleted);
    }

    /// <summary>
    /// A name with a space inside it keeps it: "Van Dyke" is one surname, and
    /// only the spaces a form puts around a value come off.
    /// </summary>
    [Fact]
    public void TrimmedKeepsTheSpacesInsideAName()
    {
        Assert.Equal("Van Dyke", new CaseSearch(LastName: " Van Dyke ").Trimmed().LastName);
    }

    /// <summary>
    /// The criteria are a record and nothing mutates: what a caller sent stays
    /// exactly as it was sent, and the trimmed criteria are a second value.
    /// </summary>
    [Fact]
    public void TrimmedLeavesTheCriteriaItWasAskedOfAlone()
    {
        var search = new CaseSearch(RecordNumber: " 2026 ", LastName: "  Rodriguez ");

        var trimmed = search.Trimmed();

        Assert.Equal(" 2026 ", search.RecordNumber);
        Assert.Equal("  Rodriguez ", search.LastName);
        Assert.Equal("2026", trimmed.RecordNumber);
        Assert.Equal("Rodriguez", trimmed.LastName);
    }
}
