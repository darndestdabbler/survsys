using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Survsys.Contracts;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// <c>Survsys.Api/SavedViewEndpoints.cs</c> against the running host: a saved
/// view is its user's alone, and its export is CSV of the chosen columns over
/// only the reports its filters select.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class SavedViewEndpointsTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string OtherPassword = "saved-view-other-password";

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task ASavedViewIsListedForTheUserWhoSavedItAndNotForAnother()
    {
        var admin = api.CreateClient();
        var view = await Created<SavedViewResponse>(admin, "/api/saved-views",
            new SaveSavedViewRequest(UniqueName(), [ExportColumns.RecordNumber]));

        var mine = (await admin.GetFromJsonAsync<PagedResult<SavedViewResponse>>("/api/saved-views", Json))!;
        Assert.Contains(mine.Items, item => item.Id == view.Id);

        using var other = await SignedInOther();
        var theirs = (await other.GetFromJsonAsync<PagedResult<SavedViewResponse>>("/api/saved-views", Json))!;
        Assert.DoesNotContain(theirs.Items, item => item.Id == view.Id);
    }

    [Fact]
    public async Task AnotherUsersViewIsA404()
    {
        var view = await Created<SavedViewResponse>(api.CreateClient(), "/api/saved-views",
            new SaveSavedViewRequest(UniqueName(), [ExportColumns.RecordNumber]));

        using var other = await SignedInOther();
        using var exported = await other.GetAsync($"/api/saved-views/{view.Id}/export");
        using var saved = await other.PutAsJsonAsync($"/api/saved-views/{view.Id}",
            new SaveSavedViewRequest(UniqueName(), [ExportColumns.LastName]), Json);
        using var deleted = await other.DeleteAsync($"/api/saved-views/{view.Id}");

        Assert.Equal(HttpStatusCode.NotFound, exported.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, saved.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
    }

    [Fact]
    public async Task TheExportIsCsvOfTheChosenColumnsOverOnlyTheReportsTheFiltersSelect()
    {
        var client = api.CreateClient();
        var person = (await client.GetFromJsonAsync<PagedResult<PersonResponse>>("/api/persons", Json))!.Items[0];
        var conditions = (await client.GetFromJsonAsync<PagedResult<ConditionResponse>>("/api/conditions?pageSize=200", Json))!
            .Items.Where(condition => condition.Active).Take(2).ToList();
        var selected = await Created<PersonConditionResponse>(client, "/api/person-conditions",
            new CreatePersonConditionRequest(person.Id, conditions[0].Id));
        var passedOver = await Created<PersonConditionResponse>(client, "/api/person-conditions",
            new CreatePersonConditionRequest(person.Id, conditions[1].Id));

        var view = await Created<SavedViewResponse>(client, "/api/saved-views",
            new SaveSavedViewRequest(UniqueName(), [ExportColumns.RecordNumber, ExportColumns.Condition, ExportColumns.LastName],
                ConditionId: conditions[0].Id));

        using var response = await client.GetAsync($"/api/saved-views/{view.Id}/export");
        var csv = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("record_number,condition,last_name", lines[0]);
        Assert.Contains(lines, line => line.StartsWith(selected.RecordNumber + ",", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.StartsWith(passedOver.RecordNumber + ",", StringComparison.Ordinal));
    }

    private static string UniqueName() => "View " + Guid.NewGuid().ToString("N");

    /// <summary>
    /// A client signed in as a second user whose role holds view_cases, created
    /// by the administrator over the wire and found again on a later run.
    /// </summary>
    private async Task<HttpClient> SignedInOther()
    {
        var admin = api.CreateClient();
        var roles = (await admin.GetFromJsonAsync<PagedResult<RoleResponse>>("/api/roles", Json))!;
        var role = roles.Items.SingleOrDefault(role => role.Name == "Case viewer")
            ?? await Created<RoleResponse>(admin, "/api/roles", new SaveRoleRequest("Case viewer", [Privileges.ViewCases, Privileges.AllJurisdictions]));

        var users = (await admin.GetFromJsonAsync<PagedResult<UserResponse>>("/api/users", Json))!;
        if (users.Items.All(user => user.UserName != "saved-view-other"))
        {
            await Created<UserResponse>(admin, "/api/users",
                new SaveUserRequest("saved-view-other", "Saved View Other", true, [role.Id], OtherPassword));
        }

        var other = api.CreateAnonymousClient();
        await api.SignIn(other, "saved-view-other", OtherPassword);

        return other;
    }

    private static async Task<T> Created<T>(HttpClient client, string url, object body)
    {
        using var response = await client.PostAsJsonAsync(url, body, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }
}
