using Survsys.Api;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The startup gate, proved without a database — which is the reason
/// <see cref="MigrationGate"/> takes a list of names rather than a
/// <c>DbContext</c>. A promise that only a deployment could test is a promise
/// nothing tests.
/// </summary>
public class MigrationGateTests
{
    [Fact]
    public void AssertNonePendingServesWhenNothingIsPending()
    {
        MigrationGate.AssertNonePending([]);
    }

    [Fact]
    public void AssertNonePendingRefusesWhenAMigrationIsPending()
    {
        var refusal = Assert.Throws<InvalidOperationException>(
            () => MigrationGate.AssertNonePending(["20261015120000_InitialCreate"]));

        Assert.Contains("20261015120000_InitialCreate", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AssertNonePendingNamesEveryPendingMigration()
    {
        string[] pending = ["20261015120000_InitialCreate", "20261116090000_AddPersons"];

        var refusal = Assert.Throws<InvalidOperationException>(
            () => MigrationGate.AssertNonePending(pending));

        Assert.All(pending, migration =>
            Assert.Contains(migration, refusal.Message, StringComparison.Ordinal));
        Assert.Contains("2 migration(s)", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The list is walked once and only once. A pending-migration list arrives
    /// from EF Core as a lazy query result, and a gate that enumerated it
    /// twice — once to count and once to name — would query twice.
    /// </summary>
    [Fact]
    public void AssertNonePendingEnumeratesThePendingListOnce()
    {
        var walks = 0;

        Assert.Throws<InvalidOperationException>(
            () => MigrationGate.AssertNonePending(Counted()));

        Assert.Equal(1, walks);

        IEnumerable<string> Counted()
        {
            walks++;
            yield return "20261015120000_InitialCreate";
        }
    }
}
