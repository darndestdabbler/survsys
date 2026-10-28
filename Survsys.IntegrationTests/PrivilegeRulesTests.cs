using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Survsys.Contracts;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// <c>Survsys.Api/PrivilegeRules.cs</c> as the running host applies it: a
/// caller who is not signed in, or who lacks the privilege a request needs, is
/// refused rather than served; and opening a report is recorded.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class PrivilegeRulesTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string ReaderPassword = "reference-reader-password";

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task ACallerNotSignedInIsAnswered401()
    {
        using var response = await api.CreateAnonymousClient().GetAsync("/api/persons");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ACallerWhoseRoleLacksViewCasesIsAnswered403OnACaseReadRatherThanServed()
    {
        using var reader = await SignedInReader();
        using var response = await reader.GetAsync("/api/persons");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ACallerWithNoPrivilegeIsServedTheReferenceData()
    {
        using var reader = await SignedInReader();
        using var response = await reader.GetAsync("/api/conditions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TheAdministratorIsServedACaseRead()
    {
        using var response = await api.CreateClient().GetAsync("/api/persons");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// The system settings were removed in session 113: there is no such
    /// route, even for the administrator who once kept them.
    /// </summary>
    [Fact]
    public async Task TheSystemSettingsRouteIsGone()
    {
        using var response = await api.CreateClient().GetAsync("/api/system-settings");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AWrongPasswordIsRefused()
    {
        using var response = await api.CreateAnonymousClient().PostAsJsonAsync(
            "/api/sign-in", new SignInRequest(SurvsysApi.AdministratorUserName, "not-the-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OpeningAReportWritesAnAccessRecordNamingTheAdministrator()
    {
        // A report opened here rather than one a fixture seeded, because the
        // report tests in this collection clear the reports they find.
        var client = api.CreateClient();
        var person = (await client.GetFromJsonAsync<PagedResult<PersonResponse>>("/api/persons", Json))!.Items[0];
        var condition = (await client.GetFromJsonAsync<PagedResult<ConditionResponse>>("/api/conditions?pageSize=200", Json))!
            .Items.First(condition => condition.Active);
        var reportId = (await Created<PersonConditionResponse>(client, "/api/person-conditions",
            new CreatePersonConditionRequest(person.Id, condition.Id))).Id;

        using (var opened = await client.GetAsync($"/api/person-conditions/{reportId}"))
        {
            opened.EnsureSuccessStatusCode();
        }

        var audit = (await client.GetFromJsonAsync<PagedResult<AccessRecordResponse>>(
            $"/api/access-audit?personConditionId={reportId}", Json))!;

        var newest = audit.Items[0];
        Assert.Equal(reportId, newest.PersonConditionId);
        Assert.Equal(SurvsysApi.AdministratorUserName, newest.UserName);
        Assert.InRange(newest.AccessedAt, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(5));
    }

    [Fact]
    public async Task SigningInWritesASignInAccessRecordNamingTheAdministrator()
    {
        var client = api.CreateClient();
        var before = await Newest(client);

        await api.SignIn(api.CreateAnonymousClient(), SurvsysApi.AdministratorUserName, SurvsysApi.AdministratorPassword);

        var after = await Newest(client);
        Assert.True(after.Id > before.Id);
        Assert.Equal("SignIn", after.Action);
        Assert.Equal(SurvsysApi.AdministratorUserName, after.UserName);
        Assert.Null(after.PersonConditionId);
    }

    [Fact]
    public async Task ExportingASavedViewWritesAnExportAccessRecordNamingTheView()
    {
        var client = api.CreateClient();
        var view = await Created<SavedViewResponse>(
            client, "/api/saved-views", new SaveSavedViewRequest("Audit export " + Guid.NewGuid(), [ExportColumns.RecordNumber]));
        var before = await Newest(client);

        using (var exported = await client.GetAsync($"/api/saved-views/{view.Id}/export"))
        {
            exported.EnsureSuccessStatusCode();
        }

        var after = await Newest(client);
        Assert.True(after.Id > before.Id);
        Assert.Equal("Export", after.Action);
        Assert.Equal($"saved view {view.Id}", after.Detail);
    }

    [Fact]
    public async Task SearchingCasesWritesASearchAccessRecordNamingTheCriteria()
    {
        var client = api.CreateClient();
        var before = await Newest(client);

        using (var searched = await client.GetAsync("/api/cases?LastName=Lovelace"))
        {
            searched.EnsureSuccessStatusCode();
        }

        var after = await Newest(client);
        Assert.True(after.Id > before.Id);
        Assert.Equal("Search", after.Action);
        Assert.Contains("LastName=Lovelace", after.Detail);
    }

    /// <summary>The newest access record, which is the one the request just made wrote.</summary>
    private static async Task<AccessRecordResponse> Newest(HttpClient client) =>
        (await client.GetFromJsonAsync<PagedResult<AccessRecordResponse>>("/api/access-audit?pageSize=1", Json))!.Items[0];

    /// <summary>
    /// A client signed in as a user whose one role holds no privilege at all,
    /// created by the administrator over the wire. The role and the user are
    /// found again on a later run rather than created twice.
    /// </summary>
    private async Task<HttpClient> SignedInReader()
    {
        var admin = api.CreateClient();
        var roles = (await admin.GetFromJsonAsync<PagedResult<RoleResponse>>("/api/roles", Json))!;
        var role = roles.Items.SingleOrDefault(role => role.Name == "Reference reader")
            ?? await Created<RoleResponse>(admin, "/api/roles", new SaveRoleRequest("Reference reader"));

        var users = (await admin.GetFromJsonAsync<PagedResult<UserResponse>>("/api/users", Json))!;
        if (users.Items.All(user => user.UserName != "reference-reader"))
        {
            await Created<UserResponse>(admin, "/api/users",
                new SaveUserRequest("reference-reader", "Reference Reader", true, [role.Id], ReaderPassword));
        }

        var reader = api.CreateAnonymousClient();
        await api.SignIn(reader,"reference-reader", ReaderPassword);

        return reader;
    }

    private static async Task<T> Created<T>(HttpClient client, string url, object body)
    {
        using var response = await client.PostAsJsonAsync(url, body, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }
}
