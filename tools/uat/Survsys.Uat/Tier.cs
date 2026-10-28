using System.Diagnostics;

namespace Survsys.Uat;

/// <summary>
/// One tier, as a process started from its build output on a given port,
/// waited on until it answers readiness. Its output goes to this console, so
/// a person sees what the tier says as it runs.
/// </summary>
internal sealed class Tier : IDisposable
{
    /// <summary>
    /// How long a tier has to answer readiness: the first start pays for JIT
    /// and for the API tier migrating an empty database.
    /// </summary>
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(2);

    private readonly Process process;
    private readonly string name;

    private Tier(string name, Process process, string baseAddress)
    {
        this.name = name;
        this.process = process;
        BaseAddress = baseAddress;
    }

    /// <summary>Where the tier listens, with the trailing slash a base address wants.</summary>
    public string BaseAddress { get; }

    public static Tier Start(string name, int port, IDictionary<string, string> environment)
    {
        var baseAddress = $"http://localhost:{port}/";
        var directory = DirectoryOf(name);
        var assembly = Path.Combine(directory, $"{name}.dll");

        if (!File.Exists(assembly))
        {
            throw new InvalidOperationException(
                $"{name} is not built at {assembly}. Survsys.Uat.csproj builds both tiers when "
                + "it builds itself, so `dotnet build tools/uat/Survsys.Uat` puts it back.");
        }

        var start = new ProcessStartInfo("dotnet")
        {
            ArgumentList = { assembly },
            WorkingDirectory = directory,
            UseShellExecute = false,
        };

        start.Environment["ASPNETCORE_URLS"] = baseAddress.TrimEnd('/');

        // Build output serves the tiers' static web assets only in the
        // Development environment, as SurvsysHarness explains; without them
        // a screen is there to read but presses nothing.
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

        foreach (var (key, value) in environment)
        {
            start.Environment[key] = value;
        }

        var tier = new Tier(name, Process.Start(start)!, baseAddress);

        try
        {
            tier.WaitUntilReady();
        }
        catch
        {
            tier.Dispose();
            throw;
        }

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
    /// The repository root, found by walking up from this assembly's output
    /// until the solution file is beside it, so the kit finds the tiers
    /// whether it runs from its own output or from a test suite's.
    /// </summary>
    public static string Root()
    {
        for (var here = new DirectoryInfo(AppContext.BaseDirectory); here is not null; here = here.Parent)
        {
            if (File.Exists(Path.Combine(here.FullName, "Survsys.sln")))
            {
                return here.FullName;
            }
        }

        throw new InvalidOperationException(
            $"No Survsys.sln above {AppContext.BaseDirectory}. Run the kit from its own build "
            + "output inside the repository.");
    }

    /// <summary>
    /// The tier's build output: <c>&lt;root&gt;/&lt;tier&gt;/bin/&lt;configuration&gt;/&lt;framework&gt;</c>,
    /// with the configuration and framework this assembly was built for.
    /// </summary>
    private static string DirectoryOf(string name)
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        return Path.Combine(Root(), name, "bin", here.Parent!.Name, here.Name);
    }

    private void WaitUntilReady()
    {
        using var asking = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var readiness = new Uri(new Uri(BaseAddress), "health/ready");
        var deadline = Stopwatch.StartNew();

        while (deadline.Elapsed < StartupTimeout)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"{name} exited {process.ExitCode} while starting, before it answered {readiness}; "
                    + "its own lines above say why.");
            }

            try
            {
                using var answer = asking.GetAsync(readiness).GetAwaiter().GetResult();

                if (answer.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (Exception notYet) when (notYet is HttpRequestException or TaskCanceledException)
            {
                // Not listening yet, or not answering yet; the deadline is
                // what makes either final.
            }

            Thread.Sleep(TimeSpan.FromMilliseconds(250));
        }

        throw new InvalidOperationException(
            $"{name} did not answer {readiness} within {StartupTimeout.TotalSeconds:0} seconds.");
    }
}
