using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// A real SQL Server database at this assembly's migration level, with the
/// condition and person fixtures in it. Built once for the whole suite.
/// </summary>
/// <remarks>
/// <strong>An unreachable server fails the suite, loudly.</strong> Nothing
/// here skips: a skipped integration suite reads as a green run, and a green
/// run that proved nothing is worse than a red one that says so. The failure
/// names the server it tried and the variable that redirects it, because the
/// one thing a developer needs on seeing it is where to point it instead.
///
/// The database is dropped and rebuilt by the migration rather than by
/// <c>EnsureCreated</c>, so what the suite runs against is the schema the
/// migration produces — which is the only schema a deployment will ever get.
/// </remarks>
public sealed class SqlServerFixture : IDisposable
{
    /// <summary>
    /// The environment variable that redirects this suite at another server.
    /// It holds a whole connection string, so a CI runner can change the
    /// authentication as well as the host.
    /// </summary>
    public const string ConnectionVariable = "SURVSYS_SQLSERVER";

    /// <summary>
    /// This host's own SQL Server, used when nothing redirects the suite.
    /// Integrated security, so there is no credential here and none to leak;
    /// the short connect timeout is so that an absent server fails in seconds
    /// rather than in the client's default quarter-minute, once per test
    /// class.
    /// </summary>
    private const string LocalDefault =
        "Server=localhost;Database=survsys_integration;Trusted_Connection=True;"
        + "TrustServerCertificate=True;Connect Timeout=5";

    public SqlServerFixture()
    {
        ConnectionString =
            Environment.GetEnvironmentVariable(ConnectionVariable) is { Length: > 0 } redirected
                ? redirected
                : LocalDefault;

        // Pooled connections from an earlier run would otherwise keep the
        // database from being dropped.
        SqlConnection.ClearAllPools();

        using var context = NewContext();

        try
        {
            context.Database.EnsureDeleted();
            context.Database.Migrate();
        }
        catch (Exception unreachable) when (unreachable is SqlException or InvalidOperationException)
        {
            throw new InvalidOperationException(Unreachable(unreachable), unreachable);
        }

        context.Database.ExecuteSqlRaw(FixtureSql("conditions.sql"));
        context.Database.ExecuteSqlRaw(FixtureSql("persons.sql"));
    }

    /// <summary>The database the suite runs against.</summary>
    public string ConnectionString { get; }

    /// <summary>
    /// How many rows <c>Fixtures/conditions.sql</c> seeds. Stated here so a
    /// test asserting a total count is not also asserting its own arithmetic.
    /// </summary>
    public const int SeededConditions = 7;

    /// <summary>
    /// The seeded conditions in the order the endpoint promises — by name.
    /// The fixture inserts them in another order on purpose, so this is a
    /// claim about the endpoint rather than about the fixture.
    /// </summary>
    public static readonly string[] ConditionsByName =
    [
        "Bartonellosis",
        "Giardiasis",
        "Hepatitis A",
        "Influenza, animal isolates",
        "Listeriosis",
        "Lyme disease",
        "Malaria",
    ];

    /// <summary>
    /// How many people <c>Fixtures/persons.sql</c> seeds. Stated here for the
    /// reason <see cref="SeededConditions"/> is.
    /// </summary>
    public const int SeededPersons = 7;

    /// <summary>
    /// The seeded people in the order the endpoint promises — by last name,
    /// then first name — written as the endpoint returns them, "Last, First".
    /// The fixture inserts them in another order on purpose, so this is a
    /// claim about the endpoint rather than about the fixture.
    /// </summary>
    /// <remarks>
    /// Two of them are the pair that makes the second key assert something:
    /// Carlos and Maria share a surname, so an endpoint that ordered by the
    /// last name alone could return them either way round. "Doe" has no first
    /// name at all, and sorts where SQL Server sorts a null — first within
    /// its surname, which is only a tie of one here.
    /// </remarks>
    public static readonly string[] PersonsByName =
    [
        "Abbott, Jane",
        "Doe, ",
        "Nguyen, An",
        "Okafor, Chidi",
        "Rodriguez, Carlos",
        "Rodriguez, Maria",
        "Zimmerman, Paul",
    ];

    /// <summary>
    /// A context over the suite's database. The caller disposes it.
    /// </summary>
    public SurvsysDbContext NewContext() => new(
        new DbContextOptionsBuilder<SurvsysDbContext>()
            .UseSqlServer(ConnectionString, sqlServer => sqlServer.CommandTimeout(60))
            .Options);

    public void Dispose() => SqlConnection.ClearAllPools();

    private static string FixtureSql(string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", file);

        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"The fixture {file} is not at {path}. It is copied to the output "
                + "directory by Survsys.IntegrationTests.csproj; a suite without it would "
                + "assert against an empty table.");
        }

        return File.ReadAllText(path);
    }

    private string Unreachable(Exception cause)
    {
        // Only the server and the database, never the connection string: a
        // redirected suite's string is whatever the runner put in the
        // environment, and it is not this message's business to repeat it.
        var where = TryDescribe(ConnectionString);

        return $"Survsys.IntegrationTests needs a reachable SQL Server and {where} did not "
            + $"answer: {cause.Message} This suite does not skip when the server is missing, "
            + "because a skipped integration suite reads as a green run. Start SQL Server, or "
            + $"set {ConnectionVariable} to the connection string of one that is running.";
    }

    private static string TryDescribe(string connectionString)
    {
        try
        {
            var parsed = new SqlConnectionStringBuilder(connectionString);
            return $"'{parsed.InitialCatalog}' on '{parsed.DataSource}'";
        }
        catch (ArgumentException)
        {
            return $"the server {ConnectionVariable} names";
        }
    }
}

/// <summary>
/// The one database, shared by every test class in the suite: building it is
/// the slowest thing here and none of these tests writes to it.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SQL Server";
}
