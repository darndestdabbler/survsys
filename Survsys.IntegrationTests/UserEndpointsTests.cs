using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Survsys.Contracts;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// <c>Survsys.Api/UserEndpoints.cs</c> against SQL Server: the investigator
/// list a workflow assignment picks from holds every active user and no
/// inactive one.
/// </summary>
/// <remarks>
/// The two users are created over the wire by the administrator, as
/// <see cref="PrivilegeRulesTests"/> creates its reader, and are found again
/// on a later run rather than created twice: a user name is unique and users
/// are deactivated rather than deleted, so nothing here clears them.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class UserEndpointsTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string ActiveUserName = "investigator-active";

    private const string SecondActiveUserName = "investigator-baker";

    private const string InactiveUserName = "investigator-inactive";

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task TheInvestigatorListHoldsAnActiveUserAndNotAnInactiveOne()
    {
        var admin = api.CreateClient();
        var active = await UserOnFile(admin, ActiveUserName, "Investigator Active", active: true);
        var inactive = await UserOnFile(admin, InactiveUserName, "Investigator Inactive", active: false);

        using var response = await admin.GetAsync("/api/investigators");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var investigators = (await response.Content.ReadFromJsonAsync<IReadOnlyList<InvestigatorResponse>>(Json))!;

        Assert.Contains(new InvestigatorResponse(active.Id, "Investigator Active"), investigators);
        Assert.DoesNotContain(investigators, investigator => investigator.Id == inactive.Id);
    }

    /// <summary>
    /// Two active users whose names differ in one letter come back in name
    /// order whatever the server's collation makes of case and punctuation,
    /// which is why the whole list is not compared against an ordinal sort.
    /// </summary>
    [Fact]
    public async Task TheInvestigatorListIsOrderedByDisplayName()
    {
        var admin = api.CreateClient();
        var able = await UserOnFile(admin, ActiveUserName, "Investigator Active", active: true);
        var baker = await UserOnFile(admin, SecondActiveUserName, "Investigator Baker", active: true);

        var investigators = (await admin.GetFromJsonAsync<IReadOnlyList<InvestigatorResponse>>("/api/investigators", Json))!;
        var ids = investigators.Select(investigator => investigator.Id).ToList();

        Assert.True(ids.IndexOf(able.Id) < ids.IndexOf(baker.Id),
            "Investigator Active should be listed before Investigator Baker.");
    }

    /// <summary>
    /// The user by that name, created through <c>POST /api/users</c> with no
    /// role where there is none yet; an existing one is saved over so its
    /// active flag is the one asked for.
    /// </summary>
    private static async Task<UserResponse> UserOnFile(HttpClient admin, string userName, string displayName, bool active)
    {
        var request = new SaveUserRequest(userName, displayName, active, [], "investigator-password");
        var users = (await admin.GetFromJsonAsync<PagedResult<UserResponse>>("/api/users", Json))!;

        if (users.Items.SingleOrDefault(user => user.UserName == userName) is { } existing)
        {
            using var saved = await admin.PutAsJsonAsync($"/api/users/{existing.Id}", request, Json);
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

            return (await saved.Content.ReadFromJsonAsync<UserResponse>(Json))!;
        }

        using var created = await admin.PostAsJsonAsync("/api/users", request, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        return (await created.Content.ReadFromJsonAsync<UserResponse>(Json))!;
    }
}
