using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace Survsys.IntegrationTests;

/// <summary>
/// The API tier as the suite runs it: the real <c>Program.cs</c>, pointed at
/// the fixture's database and started as Production, so that what the tests
/// assert is what a deployment does rather than what the developer exception
/// page does.
/// </summary>
/// <remarks>
/// Session 5 wrote this as a private class inside the conditions tests, where
/// it was the only suite there was. It is its own file now because the person
/// tests start the same host against the same database, and the one subtlety
/// in it — when the connection string has to arrive — is not a thing to hold
/// two copies of.
///
/// Starting it does not migrate anything: <c>Program.cs</c> reads the pending
/// migration list and refuses to serve while it is non-empty, so a host that
/// answers at all is a host whose database the fixture had already migrated.
/// That is an assertion in itself, and every test that starts one makes it.
/// </remarks>
public sealed class SurvsysApi : WebApplicationFactory<Program>
{
    /// <summary>
    /// The environment variable form of <c>ConnectionStrings:Survsys</c>,
    /// which is how the connection string has to arrive.
    /// </summary>
    /// <remarks>
    /// Not through <c>ConfigureAppConfiguration</c>, which would be the
    /// obvious way and does not work here: those callbacks are applied when
    /// <c>Program.cs</c> calls <c>builder.Build()</c>, and <c>Program.cs</c>
    /// reads its connection string before that, while it is still configuring
    /// services. An environment variable is read by <c>CreateBuilder</c>
    /// itself, so it is there in time.
    ///
    /// Set in the constructor rather than in a host callback for the same
    /// reason: this runs before the entry point does.
    /// </remarks>
    private const string ConnectionStringVariable = "ConnectionStrings__Survsys";

    public SurvsysApi(string connectionString) =>
        Environment.SetEnvironmentVariable(ConnectionStringVariable, connectionString);

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseEnvironment(Environments.Production);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            Environment.SetEnvironmentVariable(ConnectionStringVariable, null);
        }
    }
}
