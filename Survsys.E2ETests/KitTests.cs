using System.Net.Sockets;
using Microsoft.Data.SqlClient;
using Survsys.Uat;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The kit for performing the walkthroughs by hand, proved against the
/// harness: a reset leaves the database the main harness seeds, and a start
/// over it serves both tiers.
/// </summary>
/// <remarks>
/// The kit's database here is a scratch one beside the harness's — its name
/// with <c>_kit</c> on the end — dropped when the test ends, so the kit's own
/// default database is never touched by a test run.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class KitTests(SurvsysHarness harness)
{
    [Fact]
    public void ResetLeavesTheDatabaseTheHarnessSeeds()
    {
        KitDatabase.AssertResetMatches(harness, intake: false);
    }

    [Fact]
    public async Task StartServesBothTiersOverAResetDatabase()
    {
        var scratch = KitDatabase.ScratchOf(harness.ConnectionString);

        try
        {
            Kit.Reset(scratch, intake: false, SurvsysHarness.AdministratorUserName, SurvsysHarness.AdministratorPassword);

            using var running = Kit.Start(
                scratch,
                intake: false,
                SurvsysHarness.AdministratorUserName,
                SurvsysHarness.AdministratorPassword,
                KitDatabase.FreePort(),
                KitDatabase.FreePort());

            using var asking = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

            foreach (var tier in new[] { running.ApiAddress, running.WebAddress })
            {
                using var answer = await asking.GetAsync(new Uri(new Uri(tier), SurvsysHarness.ReadinessPath));
                Assert.True(answer.IsSuccessStatusCode, $"{tier} answered {(int)answer.StatusCode}.");
            }

            Assert.True(Directory.Exists(running.OutboxDirectory));
            Assert.Null(running.ElrDropDirectory);
            Assert.Null(running.EcrDropDirectory);
        }
        finally
        {
            KitDatabase.Drop(scratch);
        }
    }
}

/// <summary>The same proof of the reset, against the intake harness.</summary>
[Collection(IntakeCollection.Name)]
public class IntakeKitTests(IntakeHarness harness)
{
    [Fact]
    public void ResetWithIntakeLeavesTheDatabaseTheIntakeHarnessSeeds()
    {
        KitDatabase.AssertResetMatches(harness, intake: true);
    }
}

/// <summary>What the kit's tests share: the scratch database and the comparison.</summary>
internal static class KitDatabase
{
    /// <summary>
    /// The tables a seed decides the rows of: every one the clearing empties,
    /// and the account tables it clears but the administrator.
    /// </summary>
    private static readonly string[] Compared =
        [.. WalkthroughDatabase.TablesInDeletionOrder, "users", "roles", "role_privileges", "user_roles"];

    /// <summary>The harness's database with <c>_kit</c> on the end of its name.</summary>
    public static string ScratchOf(string harnessConnectionString)
    {
        var builder = new SqlConnectionStringBuilder(harnessConnectionString);
        builder.InitialCatalog += "_kit";

        return builder.ConnectionString;
    }

    /// <summary>
    /// Resets a scratch database with the kit, reseeds the harness's own, and
    /// asserts that the two hold the same number of rows in every table a
    /// seed decides. The harness is reseeded first because the walkthroughs
    /// before this test have added to it.
    /// </summary>
    public static void AssertResetMatches(SurvsysHarness harness, bool intake)
    {
        var scratch = ScratchOf(harness.ConnectionString);

        try
        {
            Kit.Reset(scratch, intake, SurvsysHarness.AdministratorUserName, SurvsysHarness.AdministratorPassword);
            harness.Seed();

            var seeded = Counts(harness.ConnectionString);
            var reset = Counts(scratch);
            var differing = Compared
                .Where(table => seeded[table] != reset[table])
                .Select(table => $"{table}: the harness holds {seeded[table]}, the kit {reset[table]}")
                .ToList();

            Assert.True(
                differing.Count == 0,
                $"The kit's reset{(intake ? " --intake" : "")} left a database the harness does not "
                + $"seed: {string.Join("; ", differing)}. Both call WalkthroughDatabase, so a "
                + "difference means one of them passes it other fixtures.");
            Assert.True(seeded.Values.Sum() > 0, "The harness's database holds no rows at all.");
        }
        finally
        {
            Drop(scratch);
        }
    }

    /// <summary>Drops the database, if it is there.</summary>
    public static void Drop(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var database = builder.InitialCatalog;
        builder.InitialCatalog = "master";

        SqlConnection.ClearAllPools();

        using var connection = new SqlConnection(builder.ConnectionString);
        connection.Open();

        using var drop = connection.CreateCommand();
        drop.CommandText =
            $"IF DB_ID(N'{database}') IS NOT NULL "
            + $"BEGIN ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]; END";
        drop.ExecuteNonQuery();
    }

    /// <summary>A port nothing is listening on, asked of the operating system.</summary>
    public static int FreePort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();

        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static Dictionary<string, int> Counts(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();

        var counts = new Dictionary<string, int>();

        foreach (var table in Compared)
        {
            using var count = connection.CreateCommand();
            count.CommandText = $"SELECT COUNT(*) FROM {table}";
            counts[table] = (int)count.ExecuteScalar()!;
        }

        return counts;
    }
}
