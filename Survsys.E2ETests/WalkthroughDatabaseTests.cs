using Survsys.Uat;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The refusals of the clearing and seeding the harness and the kit share:
/// both are made before a connection is opened, so neither needs a server.
/// What a seed leaves is proved by <see cref="SurvsysHarnessTests"/> and
/// <see cref="KitTests"/>, over a database.
/// </summary>
public class WalkthroughDatabaseTests
{
    /// <summary>
    /// The kit never recreates the harness's own databases, whatever the
    /// server and however the name is cased: a person resetting one would
    /// throw away a walkthrough run in progress.
    /// </summary>
    [Theory]
    [InlineData("survsys_e2e")]
    [InlineData("survsys_e2e_intake")]
    [InlineData("SURVSYS_E2E")]
    public void RecreateRefusesTheHarnessDatabases(string database)
    {
        var refused = Assert.Throws<InvalidOperationException>(() => WalkthroughDatabase.Recreate(
            $"Server=nowhere.invalid;Database={database};Trusted_Connection=True;Connect Timeout=1"));

        Assert.Contains(database, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RecreateRefusesAConnectionStringNamingNoDatabase()
    {
        Assert.Throws<InvalidOperationException>(() => WalkthroughDatabase.Recreate(
            "Server=nowhere.invalid;Trusted_Connection=True;Connect Timeout=1"));
    }

    /// <summary>
    /// A fixture that is not there is refused before anything is cleared, so
    /// a broken build output never leaves a database emptied and unseeded.
    /// The server named does not exist, so a seed that reached for it first
    /// would fail with a connection error instead.
    /// </summary>
    [Fact]
    public void SeedRefusesAMissingFixtureBeforeClearingAnything()
    {
        var refused = Assert.Throws<InvalidOperationException>(() => WalkthroughDatabase.Seed(
            "Server=nowhere.invalid;Database=survsys_uat;Trusted_Connection=True;Connect Timeout=1",
            Kit.FixturesDirectory,
            ["conditions.sql", "no-such-fixture.sql"],
            SurvsysHarness.AdministratorUserName));

        Assert.Contains("no-such-fixture.sql", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The account clearing keeps the administrator it is given, by name, and
    /// quotes that name so an apostrophe cannot end the literal.
    /// </summary>
    [Fact]
    public void AccountsClearedButTheAdministratorKeepsTheAdministratorNamed()
    {
        var statements = WalkthroughDatabase.AccountsClearedButTheAdministrator("o'connor");

        Assert.Contains("DELETE FROM users WHERE user_name <> N'o''connor'", statements);
        Assert.Equal("DELETE FROM roles WHERE id <> 1", statements[^1]);
    }
}
