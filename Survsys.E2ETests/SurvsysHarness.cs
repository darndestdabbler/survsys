using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// Both tiers running over a seeded database, with a browser installed to
/// drive them: the harness the walkthrough contract rests on.
/// </summary>
/// <remarks>
/// It launches the tiers as processes rather than hosting them in this one,
/// because a walkthrough describes a person using two deployed sites and that
/// is what a test of it should drive. The schema arrives through
/// <c>Survsys.Api</c> — started with its migration switch on, since it is the
/// only tier permitted to reach the database — and the rows arrive as raw SQL
/// from the fixture the integration suite also uses.
///
/// Nothing here skips. An unreachable SQL Server, a tier that does not answer
/// its readiness endpoint inside <see cref="StartupTimeout"/>, or a tier that
/// exits while starting all fail the suite loudly and name what to do about
/// it: a skipped walkthrough suite reads as a green run, and a green run that
/// proved nothing is worse than a red one that says so.
/// </remarks>
public sealed class SurvsysHarness : IDisposable
{
    /// <summary>
    /// The environment variable that redirects this suite at another SQL
    /// Server. It holds a whole connection string, so a CI runner can change
    /// the authentication as well as the host.
    /// </summary>
    public const string ConnectionVariable = "SURVSYS_E2E_SQLSERVER";

    /// <summary>
    /// How long a tier has to answer its readiness endpoint. Generous,
    /// because the first start of a run pays for JIT and for the API tier
    /// migrating an empty database; bounded, because a test never waits on a
    /// process without a timeout.
    /// </summary>
    public static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(2);

    /// <summary>
    /// The readiness endpoint both tiers answer. A 200 here means the tier
    /// has finished starting and is serving.
    /// </summary>
    public const string ReadinessPath = "health/ready";

    /// <summary>
    /// The conditions <c>Fixtures/conditions.sql</c> seeds, by name. Stated
    /// here as the integration suite states them, because this project may
    /// not reference that one, and in the order the conditions screen
    /// promises — by name — so a test asserting the screen's order is
    /// asserting something the fixture's own insert order would otherwise
    /// hide.
    /// </summary>
    public static readonly string[] SeededConditionsByName =
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
    /// This host's own SQL Server, used when nothing redirects the suite. Its
    /// own database, so a walkthrough run never disturbs the development or
    /// integration data. Integrated security, so there is no credential here
    /// and none to leak.
    /// </summary>
    private const string LocalDefault =
        "Server=localhost;Database=survsys_e2e;Trusted_Connection=True;"
        + "TrustServerCertificate=True;Connect Timeout=5";

    private readonly Tier api;
    private readonly Tier web;

    public SurvsysHarness()
    {
        InstallBrowser();

        ConnectionString =
            Environment.GetEnvironmentVariable(ConnectionVariable) is { Length: > 0 } redirected
                ? redirected
                : LocalDefault;

        // The API tier first and alone: it is what brings the database to this
        // assembly's migration level, so until it is ready there is no table
        // to seed and nothing for the application tier to read.
        api = Tier.Start("Survsys.Api", new Dictionary<string, string>
        {
            ["ConnectionStrings__Survsys"] = ConnectionString,
            ["Survsys__ApplyMigrations"] = "true",
        });

        // Whatever goes wrong from here on, the tier that is already running
        // is stopped before the failure leaves: a fixture whose constructor
        // throws is never disposed, so a leaked process would outlive the run
        // and hold the database it was given.
        try
        {
            Seed();

            web = Tier.Start("Survsys.Web", new Dictionary<string, string>
            {
                ["Survsys__ApiBaseAddress"] = api.BaseAddress,
            });
        }
        catch
        {
            api.Dispose();
            throw;
        }
    }

    /// <summary>The database the suite runs against.</summary>
    public string ConnectionString { get; }

    /// <summary>The application tier a walkthrough is performed against.</summary>
    public string WebBaseAddress => web.BaseAddress;

    /// <summary>
    /// The API tier the application tier reads through. Exposed so a test can
    /// ask it for readiness directly; a walkthrough never does.
    /// </summary>
    public string ApiBaseAddress => api.BaseAddress;

    public void Dispose()
    {
        web.Dispose();
        api.Dispose();
        SqlConnection.ClearAllPools();
    }

    /// <summary>
    /// The browser Playwright drives, installed if this host has not got it.
    /// Done here rather than left to whoever runs the suite, because a
    /// walkthrough suite that cannot run is indistinguishable from a
    /// walkthrough that does not work.
    /// </summary>
    private static void InstallBrowser()
    {
        var exit = Program.Main(["install", "chromium"]);

        if (exit != 0)
        {
            throw new InvalidOperationException(
                $"Installing Playwright's chromium exited {exit}. The walkthrough suite needs "
                + "a browser and does not skip without one; `pwsh Survsys.E2ETests/bin/Debug/"
                + "net10.0/playwright.ps1 install chromium` is the same install by hand, and "
                + "its output says what it could not fetch.");
        }
    }

    /// <summary>
    /// The condition rows, into the table the API tier has just migrated into
    /// existence. Deleted first, so a second run of the suite seeds the same
    /// seven rows rather than fourteen.
    /// </summary>
    private void Seed()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "conditions.sql");

        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"The condition fixture is not at {path}. It is linked into this project's "
                + "output from Survsys.IntegrationTests by Survsys.E2ETests.csproj; without "
                + "it the conditions screen would have no rows and a walkthrough would "
                + "assert against an empty table.");
        }

        try
        {
            using var connection = new SqlConnection(ConnectionString);
            connection.Open();

            using var clear = connection.CreateCommand();
            clear.CommandText = "DELETE FROM conditions";
            clear.ExecuteNonQuery();

            using var seed = connection.CreateCommand();
            seed.CommandText = File.ReadAllText(path);
            seed.ExecuteNonQuery();
        }
        catch (SqlException unreachable)
        {
            throw new InvalidOperationException(
                "Survsys.E2ETests needs a reachable SQL Server to seed the conditions the "
                + $"screen shows, and it could not: {unreachable.Message} This suite does "
                + "not skip when the server is missing, because a skipped walkthrough suite "
                + $"reads as a green run. Start SQL Server, or set {ConnectionVariable} to "
                + "the connection string of one that is running.",
                unreachable);
        }
    }

    /// <summary>
    /// One tier, as a process on a port found free at the time, waited on
    /// until it answers readiness.
    /// </summary>
    private sealed class Tier : IDisposable
    {
        private const int KeptOutputLines = 20;

        private readonly Process process;
        private readonly string name;
        private readonly ConcurrentQueue<string> output = new();

        private Tier(string name, Process process, string baseAddress)
        {
            this.name = name;
            this.process = process;
            BaseAddress = baseAddress;
        }

        /// <summary>Where the tier listens, with the trailing slash a base address wants.</summary>
        public string BaseAddress { get; }

        public static Tier Start(string name, IDictionary<string, string> environment)
        {
            var baseAddress = $"http://127.0.0.1:{FreePort()}/";

            var start = new ProcessStartInfo("dotnet")
            {
                // The tier as it was built, not as `dotnet run` would rebuild
                // it: a test never starts a build. Survsys.E2ETests.csproj
                // builds both tiers when it builds itself, so the assembly is
                // there by the time a test runs.
                ArgumentList = { AssemblyOf(name) },
                WorkingDirectory = DirectoryOf(name),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            start.Environment["ASPNETCORE_URLS"] = baseAddress.TrimEnd('/');

            foreach (var (key, value) in environment)
            {
                start.Environment[key] = value;
            }

            var tier = new Tier(name, Process.Start(start)!, baseAddress);

            tier.process.OutputDataReceived += tier.Keep;
            tier.process.ErrorDataReceived += tier.Keep;
            tier.process.BeginOutputReadLine();
            tier.process.BeginErrorReadLine();

            tier.WaitUntilReady();

            return tier;
        }

        public void Dispose()
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
                }
            }
            catch (InvalidOperationException)
            {
                // Already gone, which is where this was heading anyway.
            }

            process.Dispose();
        }

        /// <summary>
        /// A port nothing is listening on, asked of the operating system
        /// rather than guessed, so two runs of the suite at once do not fight
        /// over one.
        /// </summary>
        private static int FreePort()
        {
            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            return port;
        }

        /// <summary>
        /// The tier's built assembly, beside this suite's own output: same
        /// configuration, same framework, a sibling project's bin. Named
        /// rather than searched for, so a missing one says what is missing.
        /// </summary>
        private static string AssemblyOf(string name)
        {
            var assembly = Path.Combine(DirectoryOf(name), $"{name}.dll");

            if (!File.Exists(assembly))
            {
                throw new InvalidOperationException(
                    $"{name} is not built at {assembly}. Survsys.E2ETests.csproj builds both "
                    + "tiers when it builds itself, so this means the build did not run or "
                    + "its output went elsewhere: `dotnet build Survsys.sln` puts it back. "
                    + "No test here builds one, because a test that starts a build is a test "
                    + "whose own timing is a build's.");
            }

            return assembly;
        }

        /// <summary>
        /// Where that assembly lives. This suite's own output directory is
        /// <c>&lt;root&gt;/Survsys.E2ETests/bin/&lt;configuration&gt;/&lt;framework&gt;</c>,
        /// so the tier's is the same four segments with its own project name.
        /// </summary>
        private static string DirectoryOf(string name)
        {
            var here = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            var framework = here.Name;
            var configuration = here.Parent?.Name
                ?? throw new InvalidOperationException(Unexpected(here));
            var root = here.Parent?.Parent?.Parent?.Parent?.FullName
                ?? throw new InvalidOperationException(Unexpected(here));

            return Path.Combine(root, name, "bin", configuration, framework);
        }

        private static string Unexpected(DirectoryInfo here) =>
            $"The suite is running from {here.FullName}, which is not the "
            + "<root>/Survsys.E2ETests/bin/<configuration>/<framework> this harness locates "
            + "the tiers relative to. Run the suite from its own build output.";

        private void Keep(object sender, DataReceivedEventArgs line)
        {
            if (line.Data is null)
            {
                return;
            }

            output.Enqueue(line.Data);

            while (output.Count > KeptOutputLines)
            {
                output.TryDequeue(out _);
            }
        }

        /// <summary>
        /// Polls the tier's readiness endpoint until it answers, it exits, or
        /// the timeout runs out — and says which, with the tier's own last
        /// lines, because a harness that fails without them tells whoever is
        /// looking nothing.
        /// </summary>
        private void WaitUntilReady()
        {
            using var asking = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var readiness = new Uri(new Uri(BaseAddress), ReadinessPath);
            var deadline = Stopwatch.StartNew();

            while (deadline.Elapsed < StartupTimeout)
            {
                if (process.HasExited)
                {
                    throw new InvalidOperationException(
                        $"{name} exited {process.ExitCode} while starting, before it answered "
                        + $"{readiness}. Its last lines were:{NewlineJoined()}");
                }

                try
                {
                    using var answer = asking.GetAsync(readiness).GetAwaiter().GetResult();

                    if (answer.IsSuccessStatusCode)
                    {
                        return;
                    }
                }
                catch (Exception notYet) when (notYet is HttpRequestException
                                                   or TaskCanceledException)
                {
                    // Not listening yet, or not answering yet. Both are
                    // ordinary while a tier starts; the deadline is what
                    // makes either final.
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(250));
            }

            throw new InvalidOperationException(
                $"{name} did not answer {readiness} within {StartupTimeout.TotalSeconds:0} "
                + $"seconds. Its last lines were:{NewlineJoined()}");
        }

        private string NewlineJoined() =>
            output.IsEmpty
                ? " nothing at all."
                : Environment.NewLine + string.Join(Environment.NewLine, output);
    }
}

/// <summary>
/// The one harness, shared by every test in the suite: starting two tiers and
/// seeding a database is far slower than any test that follows, and a
/// walkthrough reads the screen rather than writing to it.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SurvsysCollection : ICollectionFixture<SurvsysHarness>
{
    public const string Name = "Survsys tiers";
}

/// <summary>
/// The base class every test in this suite derives from: a page in a browser
/// context that records video.
/// </summary>
/// <remarks>
/// The walkthrough contract requires that <em>every</em> end-to-end run
/// records video showing clicks and text entry, so the recording is turned on
/// here rather than per test — a test cannot forget what it never had to
/// remember. Playwright writes one file per browser context, finished when
/// the context closes, which these base classes do after each test.
///
/// The recordings land under the suite's own build output, which is not
/// tracked: a walkthrough's evidence is an artifact of a run, not a file in
/// the repository.
/// </remarks>
public class SurvsysPageTest : PageTest
{
    /// <summary>Where this run's recordings are written.</summary>
    public static string VideoDirectory { get; } =
        Path.Combine(AppContext.BaseDirectory, "videos");

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        RecordVideoDir = VideoDirectory,
    };
}
