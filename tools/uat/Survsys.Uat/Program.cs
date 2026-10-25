using Microsoft.Data.SqlClient;
using Survsys.Uat;

// The kit's commands. Everything they do is in Kit; this reads the
// environment and the arguments and says what happened.

const string ConnectionVariable = "SURVSYS_UAT_SQLSERVER";
const string UserNameVariable = "Survsys__Administrator__UserName";
const string PasswordVariable = "Survsys__Administrator__Password";

const string LocalDefault =
    "Server=localhost;Database=survsys_uat;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=5";

// The ports the walkthroughs' Before you start name, and the address
// Survsys.Web/appsettings.Development.json already points at.
const int WebPort = 5000;
const int ApiPort = 5100;

const string Usage =
    """
    Survsys.Uat: a database and both tiers for performing the walkthroughs by hand.

      reset [--intake]   Drop and recreate the database, migrate it, and seed it with
                         the main harness's fixtures, or with --intake the intake harness's.
      start [--intake]   Start Survsys.Api and Survsys.Web over it, with --intake with the
                         ELR and eCR intakes and the assignment pass on. Enter stops them.
      help               This.

    The database is SURVSYS_UAT_SQLSERVER's, or survsys_uat on this host's SQL Server.
    Survsys__Administrator__UserName and Survsys__Administrator__Password name the
    administrator you sign in as. See docs/walkthroughs/testing-by-hand.md.
    """;

var verb = args.FirstOrDefault();
var intake = args.Skip(1).Contains("--intake");

if (verb is null or "help" or "--help")
{
    Console.WriteLine(Usage);
    return 0;
}

if (verb is not ("reset" or "start"))
{
    Console.Error.WriteLine($"Unknown command {verb}.");
    Console.Error.WriteLine(Usage);
    return 1;
}

var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable) is { Length: > 0 } named
    ? named
    : LocalDefault;
var userName = Environment.GetEnvironmentVariable(UserNameVariable);
var password = Environment.GetEnvironmentVariable(PasswordVariable);

if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
{
    Console.Error.WriteLine(
        $"Set {UserNameVariable} and {PasswordVariable} first: the administrator the API tier "
        + "creates on a fresh database, and the user you sign in as.");
    return 1;
}

var database = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

try
{
    if (verb == "reset")
    {
        Kit.Reset(connectionString, intake, userName, password);

        Console.WriteLine(
            $"{database} is reset and seeded with the {(intake ? "intake" : "main")} harness's fixtures.");
        return 0;
    }

    using var running = Kit.Start(connectionString, intake, userName, password, WebPort, ApiPort);

    Console.WriteLine();
    Console.WriteLine($"Survsys is running over {database}{(intake ? ", with the intakes on" : "")}.");
    Console.WriteLine($"  Open:         {running.WebAddress}  (sign in as {userName})");
    Console.WriteLine($"  CDC outbox:   {running.OutboxDirectory}");

    if (intake)
    {
        Console.WriteLine($"  ELR drop:     {running.ElrDropDirectory}");
        Console.WriteLine($"  eCR drop:     {running.EcrDropDirectory}");
    }

    Console.WriteLine("Press Enter or Ctrl+C to stop both tiers.");

    using var stopped = new ManualResetEventSlim();
    Console.CancelKeyPress += (_, press) =>
    {
        press.Cancel = true;
        stopped.Set();
    };
    _ = Task.Run(() =>
    {
        // A console with no input answers null at once; only a line typed
        // into it stops the tiers, and Ctrl+C stops them either way.
        if (Console.ReadLine() is not null)
        {
            stopped.Set();
        }
    });

    stopped.Wait();
    Console.WriteLine("Stopping.");
    return 0;
}
catch (Exception failed) when (failed is SqlException or InvalidOperationException)
{
    Console.Error.WriteLine($"The {verb} over {database} failed: {failed.Message}");
    return 1;
}
