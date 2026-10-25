using System.Net.Sockets;

namespace Survsys.Uat;

/// <summary>
/// What the kit's commands do, as methods a test can call: a database brought
/// to the state a harness seeds.
/// </summary>
/// <remarks>
/// <c>intake</c> chooses which harness: without it the main harness's seed,
/// with it the intake harness's. One database cannot be both, because the
/// intake's assignment pass takes every staged message waiting for review —
/// the ones the main seed puts there for a person to review and promote — and
/// the intake seed's people and cases would turn up in the main walkthroughs.
/// </remarks>
public static class Kit
{
    /// <summary>The fixtures beside this assembly, where both projects that call it copy them.</summary>
    public static string FixturesDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    /// <summary>
    /// Drops and recreates the database <paramref name="connectionString"/>
    /// names, starts <c>Survsys.Api</c> over it once with migrations on — which
    /// brings it to this build's level and creates the administrator — and
    /// seeds it with the main harness's fixtures, or the intake harness's.
    /// </summary>
    public static void Reset(
        string connectionString, bool intake, string administratorUserName, string administratorPassword)
    {
        WalkthroughDatabase.Recreate(connectionString);

        using (Tier.Start("Survsys.Api", FreePort(), ApiSettings(connectionString, administratorUserName, administratorPassword)))
        {
        }

        WalkthroughDatabase.Seed(
            connectionString,
            FixturesDirectory,
            intake ? WalkthroughDatabase.IntakeFixtures : WalkthroughDatabase.MainFixtures,
            administratorUserName);
    }

    /// <summary>
    /// How often the intakes poll and the assignment pass runs, in seconds:
    /// the intake harness's, so a dropped file is taken in within moments.
    /// </summary>
    public const int PollIntervalSeconds = 2;

    /// <summary>
    /// The responsible agency the eCR fixtures' RRs name, root and extension
    /// joined by a caret, as the intake harness gives it.
    /// </summary>
    public const string ResponsibleAgencyId = "2.16.840.1.113883.4.6^8000000001";

    /// <summary>Where the kit's outbox and drop directories are made.</summary>
    public static string WorkingDirectory { get; } = Path.Combine(Path.GetTempPath(), "survsys-uat");

    /// <summary>
    /// Starts <c>Survsys.Api</c> over the database <paramref name="connectionString"/>
    /// names on <paramref name="apiPort"/>, with every setting the walkthroughs
    /// name, and <c>Survsys.Web</c> pointed at it on <paramref name="webPort"/>.
    /// With <paramref name="intake"/> the ELR and eCR intakes and the
    /// assignment pass are on, as the intake harness has them; without it they
    /// are off, as the main harness has them. The outbox and drop directories
    /// are emptied first, so nothing a previous start left is sent or taken in
    /// again.
    /// </summary>
    public static Running Start(
        string connectionString,
        bool intake,
        string administratorUserName,
        string administratorPassword,
        int webPort,
        int apiPort)
    {
        var outbox = Emptied("cdc-outbox");
        var settings = ApiSettings(connectionString, administratorUserName, administratorPassword);
        settings["CdcTransmission__Transport"] = "File";
        settings["CdcTransmission__OutboxDirectory"] = outbox;

        string? elrDrop = null;
        string? ecrDrop = null;

        if (intake)
        {
            elrDrop = Emptied("elr-drop");
            ecrDrop = Emptied("ecr-drop");

            var interval = PollIntervalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            settings["ElrIntake__DropDirectory"] = elrDrop;
            settings["ElrIntake__PollIntervalSeconds"] = interval;
            settings["MessageProcessing__PollIntervalSeconds"] = interval;
            settings["EcrIntake__TestDirectory"] = ecrDrop;
            settings["EcrIntake__PollIntervalSeconds"] = interval;
            settings["EcrIntake__ResponsibleAgencyIds__0"] = ResponsibleAgencyId;
        }

        var api = Tier.Start("Survsys.Api", apiPort, settings);

        try
        {
            var web = Tier.Start("Survsys.Web", webPort, new Dictionary<string, string>
            {
                ["Survsys__ApiBaseAddress"] = api.BaseAddress,
            });

            return new Running(api, web, outbox, elrDrop, ecrDrop);
        }
        catch
        {
            api.Dispose();
            throw;
        }
    }

    /// <summary>A directory under <see cref="WorkingDirectory"/>, made empty.</summary>
    private static string Emptied(string name)
    {
        var directory = Path.Combine(WorkingDirectory, name);

        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        Directory.CreateDirectory(directory);

        return directory;
    }

    /// <summary>The two tiers <see cref="Start"/> started, stopped together.</summary>
    public sealed class Running : IDisposable
    {
        private readonly Tier api;
        private readonly Tier web;

        internal Running(Tier api, Tier web, string outbox, string? elrDrop, string? ecrDrop)
        {
            this.api = api;
            this.web = web;
            OutboxDirectory = outbox;
            ElrDropDirectory = elrDrop;
            EcrDropDirectory = ecrDrop;
        }

        /// <summary>The application tier: the address a person opens.</summary>
        public string WebAddress => web.BaseAddress;

        /// <summary>The API tier the application tier reads through.</summary>
        public string ApiAddress => api.BaseAddress;

        /// <summary>Where a case sent to CDC is written as a file.</summary>
        public string OutboxDirectory { get; }

        /// <summary>Where a lab's message is dropped for the ELR intake; null without intake.</summary>
        public string? ElrDropDirectory { get; }

        /// <summary>Where an eICR or an RR is dropped for the eCR test feed; null without intake.</summary>
        public string? EcrDropDirectory { get; }

        public void Dispose()
        {
            web.Dispose();
            api.Dispose();
        }
    }

    /// <summary>What every start of the API tier is given.</summary>
    private static Dictionary<string, string> ApiSettings(
        string connectionString, string administratorUserName, string administratorPassword) => new()
    {
        ["ConnectionStrings__Survsys"] = connectionString,
        ["Survsys__ApplyMigrations"] = "true",
        ["Survsys__Administrator__UserName"] = administratorUserName,
        ["Survsys__Administrator__Password"] = administratorPassword,
    };

    /// <summary>A port nothing is listening on, asked of the operating system.</summary>
    private static int FreePort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();

        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }
}
