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
///
/// <see cref="IntakeHarness"/> is the same two tiers over a database of their
/// own with the ELR intake turned on, which is why the constructor that does
/// the work takes what differs between them.
/// </remarks>
public class SurvsysHarness : IDisposable
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
    /// The people <c>Fixtures/persons.sql</c> seeds, as the person search
    /// lists them when every box is empty: last name then first name, which
    /// is the order the endpoint promises. Stated here as
    /// <see cref="SeededConditionsByName"/> is, because this project may not
    /// reference the integration suite, and in the screen's order rather
    /// than the fixture's insert order for the same reason.
    /// </summary>
    public static readonly string[] SeededPeopleByName =
    [
        "Abbott, Jane",
        "Doe",
        "Nguyen, An",
        "Okafor, Chidi",
        "Rodriguez, Carlos",
        "Rodriguez, Maria",
        "Zimmerman, Paul",
    ];

    /// <summary>
    /// The staged messages <c>Fixtures/staged-messages.sql</c> seeds, by their
    /// source — the handle a walkthrough's test finds its own message by,
    /// since identity assigns the ids and a source is the one stable thing a
    /// row carries. Stated here as <see cref="SeededConditionsByName"/> is,
    /// because this project may not reference the integration suite. The order
    /// is the queue's, oldest received first: the exception, the QA-flagged
    /// message, the message to promote and the one the duplicate walkthrough
    /// promotes and revisits.
    /// </summary>
    public static readonly string[] SeededStagedMessageSources =
    [
        "elr-exception-example.hl7",
        "elr-qa-example.hl7",
        "elr-promote-example.hl7",
        "elr-duplicate-example.hl7",
    ];

    /// <summary>
    /// The notification guide <c>Fixtures/notification-guide.sql</c> seeds,
    /// with Lyme disease exported under it: the guide the CDC walkthroughs'
    /// messages are built under.
    /// </summary>
    public const string SeededGuide = "Walkthrough guide";

    /// <summary>
    /// The folder the API tier's CDC file transport writes each message into:
    /// the harness turns the file transport on, so "process queue now" sends a
    /// case without anything leaving this host. Emptied before the tier starts,
    /// so what is in it is what this run sent.
    /// </summary>
    public static string OutboxDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "cdc-outbox");

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
    private readonly string[] seeded;

    public SurvsysHarness()
        : this(ConfiguredConnectionString(), Fixtures, SignedInStatePath, Settings())
    {
    }

    /// <summary>
    /// Both tiers over <paramref name="connectionString"/>, seeded from
    /// <paramref name="fixtures"/>, signed in with the state kept at
    /// <paramref name="signedInStatePath"/>, and the API tier given
    /// <paramref name="apiSettings"/> beside the settings every harness gives it.
    /// </summary>
    protected SurvsysHarness(
        string connectionString,
        string[] fixtures,
        string signedInStatePath,
        IDictionary<string, string> apiSettings)
    {
        InstallBrowser();

        ConnectionString = connectionString;
        seeded = fixtures;

        var settings = new Dictionary<string, string>
        {
            ["ConnectionStrings__Survsys"] = ConnectionString,
            ["Survsys__ApplyMigrations"] = "true",
            ["Survsys__Administrator__UserName"] = AdministratorUserName,
            ["Survsys__Administrator__Password"] = AdministratorPassword,
        };

        foreach (var (key, value) in apiSettings)
        {
            settings[key] = value;
        }

        // The API tier first and alone: it is what brings the database to this
        // assembly's migration level, so until it is ready there is no table
        // to seed and nothing for the application tier to read.
        api = Tier.Start("Survsys.Api", settings);

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

        try
        {
            SignIn(signedInStatePath).GetAwaiter().GetResult();
        }
        catch
        {
            web.Dispose();
            api.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The administrator the API tier is started with and creates on its
    /// first start against this suite's database: the user every walkthrough
    /// is signed in as, because its role holds every privilege.
    /// </summary>
    public const string AdministratorUserName = "administrator";

    /// <summary>
    /// The administrator's password: a value made up for this suite's own
    /// database, and no credential of anything else.
    /// </summary>
    public const string AdministratorPassword = "walkthrough-only-password";

    /// <summary>
    /// The signed-in browser state every walkthrough's context opens with,
    /// written once by the harness: what each walkthrough's Before you start
    /// asks of its reader, met before step 1 as the seeded rows are.
    /// </summary>
    public static string SignedInStatePath { get; } =
        Path.Combine(AppContext.BaseDirectory, "signed-in-state.json");

    /// <summary>The database the suite runs against.</summary>
    public string ConnectionString { get; }

    /// <summary>
    /// The database the main harness runs against: the one
    /// <see cref="ConnectionVariable"/> names, or this host's own.
    /// </summary>
    public static string ConfiguredConnectionString() =>
        Environment.GetEnvironmentVariable(ConnectionVariable) is { Length: > 0 } redirected
            ? redirected
            : LocalDefault;

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
    /// Signs a browser in as the administrator through the application tier's
    /// own Sign in page, as a reader of a walkthrough does, and keeps the
    /// cookie it is given at <paramref name="signedInStatePath"/>.
    /// </summary>
    private async Task SignIn(string signedInStatePath)
    {
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        await using var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(WebBaseAddress);
        await page.Locator("#sign-in-user-name").FillAsync(AdministratorUserName);
        await page.Locator("#sign-in-password").FillAsync(AdministratorPassword);
        await page.Locator("#sign-in-submit").ClickAsync();

        try
        {
            await page.WaitForURLAsync(url => !url.Contains("/sign-in", StringComparison.Ordinal));
        }
        catch (TimeoutException refused)
        {
            throw new InvalidOperationException(
                $"Signing in as {AdministratorUserName} did not leave the Sign in page. The API tier "
                + "creates that administrator on its first start from the settings the harness "
                + "gives it; a database where a user of that name already has another password "
                + "keeps that password, and the page would say the sign-in was refused.",
                refused);
        }

        await context.StorageStateAsync(new() { Path = signedInStatePath });
    }

    /// <summary>
    /// The four fixtures this suite seeds, in the order they run: the
    /// conditions first, because a report names one of them, then the people,
    /// then the staged messages the ELR walkthroughs act on — which name
    /// neither a person nor a condition the schema enforces — and last the
    /// notification guide, which names a condition the first has seeded.
    /// </summary>
    private static readonly string[] Fixtures = ["conditions.sql", "persons.sql", "staged-messages.sql", "notification-guide.sql"];

    /// <summary>
    /// The stand-in for IBIS the main harness's API tier sends to, started by
    /// <see cref="Settings"/>. It is not stopped with a harness, because the
    /// intake harness's disposal runs this class's while the main harness's
    /// tests may still be sending; its socket closes when the test run's
    /// process exits.
    /// </summary>
    private static IbisStandIn? ibis;

    /// <summary>
    /// What the main harness gives the API tier: the CDC file transport,
    /// writing into <see cref="OutboxDirectory"/>, emptied here, and an IBIS
    /// endpoint that is a stand-in on this host's loopback, so a walkthrough
    /// sends a case to IBIS without anything leaving this host. The queue
    /// worker stays off, as the shipped configuration leaves it, so a case is
    /// sent when a walkthrough presses Process now and not before.
    /// </summary>
    private static Dictionary<string, string> Settings()
    {
        if (Directory.Exists(OutboxDirectory))
        {
            Directory.Delete(OutboxDirectory, recursive: true);
        }

        Directory.CreateDirectory(OutboxDirectory);

        ibis = IbisStandIn.Start();

        return new Dictionary<string, string>
        {
            ["CdcTransmission__Transport"] = "File",
            ["CdcTransmission__OutboxDirectory"] = OutboxDirectory,
            ["IbisTransmission__Endpoint"] = ibis.Url,
        };
    }

    /// <summary>
    /// A stand-in for IBIS on an OS-assigned loopback port, answering every
    /// request <c>200 OK</c> — which is all the API tier's transmitter asks of
    /// IBIS to call a case accepted. Plain sockets rather than a web host,
    /// because this project hosts nothing else and one fixed answer needs no
    /// more; it does not model IBIS refusing a case or timing out.
    /// </summary>
    private sealed class IbisStandIn
    {
        private readonly TcpListener listener;

        private IbisStandIn(TcpListener listener)
        {
            this.listener = listener;
            Url = $"http://127.0.0.1:{((System.Net.IPEndPoint)listener.LocalEndpoint).Port}/";
        }

        /// <summary>The address the API tier posts each case to.</summary>
        public string Url { get; }

        public static IbisStandIn Start()
        {
            var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var standIn = new IbisStandIn(listener);
            _ = standIn.Accept();

            return standIn;
        }

        private async Task Accept()
        {
            while (true)
            {
                TcpClient client;

                try
                {
                    client = await listener.AcceptTcpClientAsync();
                }
                catch (Exception stopped) when (stopped is SocketException or ObjectDisposedException)
                {
                    return;
                }

                _ = Answer(client);
            }
        }

        /// <summary>
        /// Reads one request — its headers, then as much body as they say —
        /// so the reply never cuts off a request still being sent, and
        /// answers it accepted.
        /// </summary>
        private static async Task Answer(TcpClient client)
        {
            using (client)
            {
                var stream = client.GetStream();
                var received = new List<byte>();
                var buffer = new byte[4096];
                var headersEnd = -1;
                var length = 0;

                while (headersEnd < 0 || received.Count < headersEnd + length)
                {
                    var read = await stream.ReadAsync(buffer);

                    if (read == 0)
                    {
                        return;
                    }

                    received.AddRange(buffer.AsSpan(0, read).ToArray());

                    if (headersEnd < 0)
                    {
                        var text = System.Text.Encoding.ASCII.GetString(received.ToArray());
                        var end = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);

                        if (end >= 0)
                        {
                            headersEnd = end + 4;
                            length = text[..end].Split("\r\n")
                                .Where(header => header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                                .Select(header => int.Parse(header["Content-Length:".Length..].Trim()))
                                .FirstOrDefault();
                        }
                    }
                }

                await stream.WriteAsync("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray());
            }
        }
    }

    /// <summary>
    /// The tables the fixtures fill, and the ones that hang off them, in the
    /// order they can be emptied: each table before the one it references,
    /// so the laboratory rows go before the reports and the people they
    /// name, the reports before the people, and the people before the
    /// conditions. Cleared before any insert, so a second run of the suite
    /// seeds the same seven people, seven conditions and one guide rather than
    /// fourteen of each — and no report, export, queued notification, specimen, exposure,
    /// note, task, attachment, facility, saved view, form, vaccine record,
    /// outbreak or treatment at all, since a
    /// walkthrough that needs one creates it, and one left over from the last
    /// run would be one the document did not describe.
    /// </summary>
    /// <remarks>
    /// The laboratory chain is listed in full rather than left to the
    /// database's cascades: a specimen names its person with a reference the
    /// schema refuses to cascade, so a specimen left behind would refuse the
    /// clearing of the people, and the run after a laboratory walkthrough
    /// would fail to seed. Results go before tests, tests and susceptibility
    /// rows before specimens, the links and the shares before the reports
    /// they hang off, and specimens before the people they name.
    /// <para>
    /// The CDC export log (<c>nmi_log</c>) and the notification queue
    /// (<c>nmi_export_queue</c>) both name their report with a reference the
    /// schema cascades, so the reports' clearing would take them anyway; they
    /// are listed before <c>person_conditions</c> so the order says what it
    /// clears rather than leaving it to the cascade. The queue is filled by
    /// the walkthroughs that queue a case, and the log by the queue worker when
    /// they press Process now.
    /// </para>
    /// <para>
    /// The notification guides go after the people and before the conditions:
    /// <c>Fixtures/notification-guide.sql</c> reseeds the one the CDC
    /// walkthroughs send under, and the guide walkthrough adds one of its own.
    /// A guide's condition, variables, links and header values name it by bare
    /// ids or by references the schema refuses to cascade, so they go first.
    /// The PHIN variable catalogue follows the guides that carry its variables:
    /// no migration seeds it, and the guide walkthrough adds the variable it
    /// gives its guide, which a second run could not add again.
    /// </para>
    /// <para>
    /// The exposure chain is listed in full for the same reason, and it is
    /// the one phase 3 brought that matters most: an exposure contact names
    /// its person with a reference the schema refuses to cascade, so a
    /// contact left behind would refuse the clearing of the people and the
    /// run after a contacts walkthrough would fail to seed. The contacts and
    /// the sources go before the exposure they hang off, and the exposure
    /// before the place it names by <c>exposure_place_id</c>. An attachment
    /// goes before the <c>attachment_files</c> row holding its bytes, which
    /// is the row it names. The notes and the tasks name nothing the schema
    /// enforces, and are listed above the reports they are raised against
    /// because that is where they belong in this order. A facility names its
    /// agency by a reference the schema enforces, but the agencies are the
    /// migration's and the suite never clears them, so a facility is cleared
    /// on its own and listed first, where nothing the suite clears hangs off
    /// it. A saved view is cleared the same way and for the same reason: it
    /// names its user by a reference the schema enforces, but the API tier
    /// creates the administrator and the suite never clears the users.
    /// </para>
    /// <para>
    /// Phase 7's tables are listed for the reason the laboratory chain is:
    /// each names something the suite clears by a reference the schema
    /// refuses to cascade. A form, an outbreak and a treatment's condition
    /// list each name their condition that way, so one left behind would
    /// refuse the clearing of the conditions; an answer names its question
    /// and an assignment its form the same way, so the answers and the
    /// assignments go before the form tree, and the tree is listed leaf
    /// first, options, questions and sections before the form. A vaccine
    /// record and a form assignment hang off their report and go before it.
    /// The outbreaks go after the reports, because a report names its
    /// outbreak, with the outbreak's access, report sections and activity
    /// before it; the treatments go after their condition lists. The vaccines
    /// catalogue and the report sections are the migration's and are kept,
    /// as the agencies are.
    /// </para>
    /// <para>
    /// The staged messages phase 5 brought are listed last, after the
    /// conditions and the people: <c>system_messages</c> names neither a
    /// person nor a condition the schema enforces and nothing hangs off it, so
    /// its place in this order is free, and last is where it sits with the
    /// other tables the fixtures fill and keep. It is cleared for a second
    /// reason the conditions and the people are not — a promoted message is
    /// moved to the assigned state in place, so a run after a promotion
    /// walkthrough would start from a queue that is already a case rather than
    /// from the seed, and the clearing is what puts it back.
    /// </para>
    /// <para>
    /// The vocabulary, its rules and the zip-to-agency mapping phase 10's
    /// intake reads go first: a lab's own codes name their facility, and the
    /// master LOINC rows and the rules name their condition, by references the
    /// schema refuses to cascade. <c>Fixtures/elr-intake.sql</c> fills them for
    /// <see cref="IntakeHarness"/>; for this harness they start empty, and the
    /// LOINC walkthrough adds what it needs.
    /// </para>
    /// </remarks>
    public static readonly string[] TablesInDeletionOrder =
    [
        "vocab_rule_conditions",
        "vocab_rules",
        "vocab_child_vocab",
        "vocab_child_loinc",
        "vocab_master_loinc",
        "vocab_master_vocab",
        "vocab_master_organism",
        "zip_code_agencies",
        "facilities",
        "saved_views",
        "attachments",
        "attachment_files",
        "notes",
        "tasks",
        "exposure_contacts",
        "exposure_sources",
        "exposures",
        "exposure_places",
        "person_condition_shares",
        "person_condition_labs",
        "lab_test_results",
        "lab_tests",
        "lab_resist_tests",
        "labs",
        "nmi_log",
        "nmi_export_queue",
        "form_answers",
        "form_assignments",
        "form_question_options",
        "form_questions",
        "form_sections",
        "forms",
        "person_vaccines",
        "person_treatments",
        "encounters",
        "person_conditions",
        "outbreak_access",
        "outbreak_report_sections",
        "outbreak_updates",
        "outbreaks",
        "condition_treatments",
        "treatments",
        "parent_guardians",
        "person_races",
        "person_aliases",
        "person_emails",
        "person_telephones",
        "person_addresses",
        "persons",
        "condition_detail",
        "guide_phin_var",
        "guide_link",
        "nmi_guide_variable",
        "guide",
        "phin_var",
        "conditions",
        "system_messages",
    ];

    /// <summary>
    /// The condition and person rows, into the tables the API tier has just
    /// migrated into existence, after everything those tables and their
    /// dependants held from the last run is deleted. Run once by the
    /// constructor, and public so that <see cref="SurvsysHarnessTests"/> can
    /// run it again over whatever the walkthroughs before it left behind and
    /// prove that the clearing still holds.
    /// </summary>
    public void Seed()
    {
        var paths = seeded
            .Select(fixture => Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture))
            .ToList();

        foreach (var path in paths.Where(path => !File.Exists(path)))
        {
            throw new InvalidOperationException(
                $"The fixture is not at {path}. {string.Join(", ", seeded)} are copied into "
                + "this project's output by Survsys.E2ETests.csproj, most of them linked from "
                + "Survsys.IntegrationTests; without one the screen it fills would have no rows "
                + "and a walkthrough would assert against an empty table.");
        }

        try
        {
            using var connection = new SqlConnection(ConnectionString);
            connection.Open();

            foreach (var table in TablesInDeletionOrder)
            {
                using var clear = connection.CreateCommand();
                clear.CommandText = $"DELETE FROM {table}";
                clear.ExecuteNonQuery();
            }

            foreach (var path in paths)
            {
                using var seed = connection.CreateCommand();
                seed.CommandText = File.ReadAllText(path);
                seed.ExecuteNonQuery();
            }
        }
        catch (SqlException unreachable)
        {
            throw new InvalidOperationException(
                "Survsys.E2ETests needs a reachable SQL Server to seed the rows the screens "
                + $"show, from {string.Join(", ", seeded)}, and it "
                + $"could not: {unreachable.Message} This suite does not skip when the "
                + "server is missing, because a skipped walkthrough suite reads as a green "
                + $"run. Start SQL Server, or set {ConnectionVariable} to the connection "
                + "string of one that is running.",
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

            // The tier runs from build output, and build output serves its
            // static web assets -- Blazor's own script, MudBlazor's script
            // and stylesheet -- only in the Development environment; in any
            // other it answers those requests with nothing, the circuit never
            // starts, and a screen is there to read but presses nothing.
            // `dotnet run` would set this from launchSettings.json; the
            // harness starts the assembly directly, so it sets it here, and
            // does not leave it to whatever the shell that ran the suite
            // happened to have. Neither tier branches on the environment, and
            // the two Development settings files hold only values the
            // environment below overrides.
            start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

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
/// Both tiers with the inbound ELR intake turned on: a drop directory polled
/// every few seconds, and the assignment pass that assigns, graylists and
/// releases what it stages. What the intake walkthroughs are performed
/// against.
/// </summary>
/// <remarks>
/// Its database is its own — the main harness's name with <c>_intake</c>
/// appended — because the assignment pass takes every message queued for
/// review, and over the main harness's database it would move the seeded
/// messages the review, promote and duplicate walkthroughs read before they
/// read them. It is seeded from the conditions, the people and
/// <c>Fixtures/elr-intake.sql</c>, and no staged message: what is in its
/// queue is what a walkthrough dropped.
/// </remarks>
public sealed class IntakeHarness : SurvsysHarness
{
    /// <summary>
    /// How often the drop directory is polled and the assignment pass runs, in
    /// seconds: short, so a walkthrough waits on the queue for moments rather
    /// than the two minutes a deployment's default would take.
    /// </summary>
    public const int PollIntervalSeconds = 2;

    /// <summary>The fixtures this harness seeds, in the order they run.</summary>
    public static readonly string[] IntakeFixtures = ["conditions.sql", "persons.sql", "elr-intake.sql"];

    /// <summary>
    /// The signed-in browser state this harness's tests open with: a file of
    /// its own, because both harnesses' application tiers listen on
    /// <c>127.0.0.1</c> and the two collections run at once.
    /// </summary>
    public static string IntakeSignedInStatePath { get; } =
        Path.Combine(AppContext.BaseDirectory, "intake-signed-in-state.json");

    private static readonly string DropDirectory = Path.Combine(AppContext.BaseDirectory, "intake-drop");

    public IntakeHarness()
        : base(
            IntakeConnectionString(ConfiguredConnectionString()),
            IntakeFixtures,
            IntakeSignedInStatePath,
            Settings())
    {
    }

    /// <summary>
    /// The directory the API tier's intake polls: a walkthrough's reader
    /// copies a message here. Emptied before the tier starts, so nothing a
    /// previous run dropped is staged again.
    /// </summary>
    public string IntakeDirectory => DropDirectory;

    /// <summary>
    /// The database this harness runs against, given the main harness's:
    /// the same server and sign-in, the database name with <c>_intake</c> on
    /// the end.
    /// </summary>
    public static string IntakeConnectionString(string main)
    {
        var builder = new SqlConnectionStringBuilder(main);
        builder.InitialCatalog += "_intake";

        return builder.ConnectionString;
    }

    /// <summary>One of the messages under <c>Fixtures/Hl7</c>, by file name.</summary>
    public static string Message(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Hl7", name);

    private static Dictionary<string, string> Settings()
    {
        if (Directory.Exists(DropDirectory))
        {
            Directory.Delete(DropDirectory, recursive: true);
        }

        Directory.CreateDirectory(DropDirectory);

        var interval = PollIntervalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return new Dictionary<string, string>
        {
            ["ElrIntake__DropDirectory"] = DropDirectory,
            ["ElrIntake__PollIntervalSeconds"] = interval,
            ["MessageProcessing__PollIntervalSeconds"] = interval,
        };
    }
}

/// <summary>The intake harness, shared by every intake walkthrough.</summary>
[CollectionDefinition(Name)]
public sealed class IntakeCollection : ICollectionFixture<IntakeHarness>
{
    public const string Name = "Survsys tiers with the ELR intake";
}

/// <summary>
/// The base class an intake walkthrough's test derives from: a
/// <see cref="SurvsysPageTest"/> signed in to <see cref="IntakeHarness"/>.
/// </summary>
public class IntakePageTest : SurvsysPageTest
{
    public override BrowserNewContextOptions ContextOptions() => new()
    {
        RecordVideoDir = VideoDirectory,
        StorageStatePath = IntakeHarness.IntakeSignedInStatePath,
    };
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

    /// <remarks>
    /// Every context opens signed in as the administrator, with the state
    /// the harness wrote when it signed in through the Sign in page.
    /// </remarks>
    public override BrowserNewContextOptions ContextOptions() => new()
    {
        RecordVideoDir = VideoDirectory,
        StorageStatePath = SurvsysHarness.SignedInStatePath,
    };

    /// <summary>
    /// Opens a screen and waits until it is interactive.
    /// </summary>
    /// <remarks>
    /// <c>Survsys.Web</c> prerenders every screen and then connects the
    /// interactive circuit, so for a moment after the page has loaded the
    /// screen is there to read but a click on it reaches nothing: the
    /// circuit that would handle it is not yet attached. A walkthrough that
    /// only reads never notices; one that presses a button presses it into
    /// that gap and sees nothing happen, which is exactly the render-wait
    /// flakiness the solution plan named as the enemy of this suite.
    ///
    /// The wait is for a signal, never for a time. When Blazor's renderer
    /// renders a component interactively it marks every element the
    /// component holds a reference to with a <c>_bl_&lt;n&gt;</c> attribute;
    /// prerendered markup never carries one, because it is produced on the
    /// server with no renderer in the browser. MudBlazor's buttons and text
    /// boxes hold such references and the application bar is on every
    /// screen, so the first of those attributes to appear is the moment the
    /// circuit has rendered the screen and will answer a click.
    /// </remarks>
    protected async Task Open(string address)
    {
        await Page.GotoAsync(address);

        try
        {
            await Page.WaitForFunctionAsync(InteractivelyRendered);
        }
        catch (TimeoutException never)
        {
            throw new InvalidOperationException(
                $"{address} loaded but never became interactive: no element on it was "
                + "rendered by Blazor's circuit. The screen is there to read, so the tier "
                + "is serving; what is missing is the script that starts the circuit or "
                + "the circuit itself. A tier run from build output serves Blazor's own "
                + "script and MudBlazor's assets only in the Development environment, "
                + "which the harness sets when it starts the tier; a browser console "
                + "would show the script arriving empty, or the circuit failing to "
                + "connect.",
                never);
        }
    }

    /// <summary>
    /// One numbered step of a walkthrough document, performed.
    /// </summary>
    /// <remarks>
    /// This is the suite's contract that a failure names the walkthrough and
    /// the step: whatever goes wrong inside the step leaves naming the
    /// document and the step number, because "assertion failed" tells whoever
    /// is looking nothing about which written step of a walkthrough stopped
    /// working — and the step that stopped working is what someone has to go
    /// and read. Shared here so that every walkthrough test fails in the same
    /// words rather than each carrying a copy of them.
    /// </remarks>
    protected static async Task Step(string document, int number, string what, Func<Task> perform)
    {
        try
        {
            await perform();
        }
        catch (Exception failed)
        {
            throw new InvalidOperationException(
                $"{document} step {number} ({what}) could not be performed: {failed.Message} "
                + "A step this test cannot perform is a defect in the application or in that "
                + "document, never a step to skip or to perform another way.",
                failed);
        }
    }

    /// <summary>
    /// True once any element on the screen carries the mark Blazor's
    /// interactive renderer leaves and prerendering does not.
    /// </summary>
    private const string InteractivelyRendered =
        "() => Array.from(document.querySelectorAll('*'))"
        + ".some(element => Array.from(element.attributes)"
        + ".some(attribute => attribute.name.startsWith('_bl_')))";
}
