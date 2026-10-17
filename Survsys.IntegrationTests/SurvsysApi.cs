using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Survsys.Contracts;

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

    /// <summary>
    /// The administrator the API tier is started with, which it creates on
    /// first start against the fixture's database. Environment variables for
    /// the reason the connection string is one: <c>Program.cs</c> reads them
    /// before <c>builder.Build()</c>.
    /// </summary>
    private const string AdministratorUserNameVariable = "Survsys__Administrator__UserName";
    private const string AdministratorPasswordVariable = "Survsys__Administrator__Password";

    /// <summary>The administrator's user name, holding every privilege.</summary>
    public const string AdministratorUserName = "integration-admin";

    /// <summary>
    /// The administrator's password: a value made up for this suite's own
    /// throwaway database, and no credential of anything else.
    /// </summary>
    public const string AdministratorPassword = "integration-only-password";

    public SurvsysApi(string connectionString)
    {
        Environment.SetEnvironmentVariable(ConnectionStringVariable, connectionString);
        Environment.SetEnvironmentVariable(AdministratorUserNameVariable, AdministratorUserName);
        Environment.SetEnvironmentVariable(AdministratorPasswordVariable, AdministratorPassword);
    }

    /// <summary>
    /// A client of the host that has not signed in, for a test of what such a
    /// caller is refused. <see cref="WebApplicationFactory{TEntryPoint}.CreateClient()"/>
    /// signs in as the administrator.
    /// </summary>
    public HttpClient CreateAnonymousClient() => Server.CreateClient();

    /// <summary>
    /// Signs this client in as the user named, so every request it makes
    /// carries that user's bearer token.
    /// </summary>
    /// <remarks>
    /// The token is asked for on a client of its own, because a client's
    /// default headers cannot be changed once it has sent a request.
    /// </remarks>
    public async Task SignIn(HttpClient client, string userName, string password)
    {
        using var signingIn = Server.CreateClient();
        using var response = await signingIn.PostAsJsonAsync("/api/sign-in", new SignInRequest(userName, password));
        response.EnsureSuccessStatusCode();

        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseEnvironment(Environments.Production);

    /// <summary>
    /// Every client the suite creates is signed in as the administrator, so a
    /// test of what an endpoint serves is not also a test of who may ask.
    /// </summary>
    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);

        SignIn(client, AdministratorUserName, AdministratorPassword).GetAwaiter().GetResult();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            Environment.SetEnvironmentVariable(ConnectionStringVariable, null);
            Environment.SetEnvironmentVariable(AdministratorUserNameVariable, null);
            Environment.SetEnvironmentVariable(AdministratorPasswordVariable, null);
        }
    }
}
