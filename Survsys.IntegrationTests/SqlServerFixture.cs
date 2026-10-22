using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Survsys.Data;
using Testcontainers.MsSql;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// A real SQL Server database at this assembly's migration level, with the
/// condition and person fixtures in it. Built once for the whole suite.
/// </summary>
/// <remarks>
/// <strong>The suite brings its own server.</strong> Unless
/// <see cref="ConnectionVariable"/> names one, the fixture starts a SQL Server
/// 2022 container for the run, through whatever Docker-compatible engine
/// Testcontainers finds (Docker Desktop, or Podman with <c>DOCKER_HOST</c>
/// pointing at it), and removes it when the run ends. Its password is the one
/// Testcontainers generates for that container, so there is none to keep.
///
/// <strong>An unreachable engine or server fails the suite, loudly.</strong>
/// Nothing here skips: a skipped integration suite reads as a green run, and a
/// green run that proved nothing is worse than a red one that says so. The
/// failure names what it tried and the variables that redirect it, because the
/// one thing a developer needs on seeing it is where to point it instead.
///
/// The database is dropped and rebuilt by the migration rather than by
/// <c>EnsureCreated</c>, so what the suite runs against is the schema the
/// migration produces — which is the only schema a deployment will ever get.
/// </remarks>
public sealed class SqlServerFixture : IAsyncLifetime
{
    /// <summary>
    /// The environment variable that redirects this suite at another server.
    /// It holds a whole connection string, so a CI runner can change the
    /// authentication as well as the host.
    /// </summary>
    public const string ConnectionVariable = "SURVSYS_SQLSERVER";

    /// <summary>The variable that points Testcontainers at a container engine.</summary>
    private const string EngineVariable = "DOCKER_HOST";

    private const string Image = "mcr.microsoft.com/mssql/server:2022-latest";

    private MsSqlContainer? container;

    public async Task InitializeAsync()
    {
        ConnectionString =
            Environment.GetEnvironmentVariable(ConnectionVariable) is { Length: > 0 } redirected
                ? redirected
                : await StartContainer();

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
        context.Database.ExecuteSqlRaw(FixtureSql("person-conditions.sql"));
    }

    /// <summary>The database the suite runs against.</summary>
    public string ConnectionString { get; private set; } = "";

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
    /// The record number of the one case morbidity report
    /// <c>Fixtures/person-conditions.sql</c> seeds — the report the laboratory
    /// round-trip records a specimen against. Stated here so a test naming it
    /// is not also restating the fixture's own value, and echoing the report
    /// <c>../nedss/docs/db-snapshots/session-011/02-after-lab-save.txt</c>
    /// dumps.
    /// </summary>
    public const string SeededReportRecordNumber = "202600004";

    /// <summary>
    /// The seeded report's person, as <see cref="PersonsByName"/> writes them:
    /// the person a specimen recorded against the report is collected from.
    /// </summary>
    public const string SeededReportPerson = "Rodriguez, Maria";

    /// <summary>
    /// The seeded report's condition, as <see cref="ConditionsByName"/> lists
    /// it — an active one, which is the only kind a report may be opened for.
    /// </summary>
    public const string SeededReportCondition = "Malaria";

    /// <summary>
    /// A context over the suite's database. The caller disposes it.
    /// </summary>
    /// <remarks>
    /// Its changes are audited as made by <paramref name="userId"/>, when given.
    /// </remarks>
    public SurvsysDbContext NewContext(int? userId = null) => new(
        new DbContextOptionsBuilder<SurvsysDbContext>()
            .UseSqlServer(ConnectionString, sqlServer => sqlServer.CommandTimeout(60))
            .Options,
        userId is null ? null : new FixedAuditUser(userId));

    private sealed record FixedAuditUser(int? UserId) : IAuditUser;

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();

        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    /// <summary>
    /// Starts the run's SQL Server container and returns the suite's
    /// connection string into it. The short connect timeout is so that a
    /// server that went away fails in seconds rather than in the client's
    /// default quarter-minute, once per test class.
    /// </summary>
    private async Task<string> StartContainer()
    {
        try
        {
            container = new MsSqlBuilder(Image).Build();
            await container.StartAsync();
        }
        catch (DockerUnavailableException unavailable)
        {
            throw new InvalidOperationException(NoEngine(unavailable), unavailable);
        }

        return new SqlConnectionStringBuilder(container.GetConnectionString())
        {
            InitialCatalog = "survsys_integration",
            ConnectTimeout = 5,
        }.ConnectionString;
    }

    private static string NoEngine(Exception cause)
    {
        var tried = TestcontainersSettings.OS.DockerEndpointAuthConfig?.Endpoint?.ToString()
            ?? "no endpoint at all";
        var dockerHost = Environment.GetEnvironmentVariable(EngineVariable) is { Length: > 0 } host
            ? $"'{host}'"
            : "not set";

        return $"Survsys.IntegrationTests starts its own SQL Server container and no container "
            + $"engine answered at {tried} ({EngineVariable} is {dockerHost}): {cause.Message} "
            + "This suite does not skip when the engine is missing, because a skipped integration "
            + $"suite reads as a green run. Start Docker or Podman, set {EngineVariable} to its "
            + $"endpoint, or set {ConnectionVariable} to the connection string of a running SQL Server.";
    }

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
