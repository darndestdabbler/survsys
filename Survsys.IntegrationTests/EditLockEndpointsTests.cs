using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// A case's edit lock against the real database: taken by one user, seen and
/// refused by a second, released and taken by the second, and lapsed after its
/// timeout.
/// </summary>
/// <remarks>
/// Each test opens its own case and deletes it after, so a lock one test
/// leaves behind cannot decide another's.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class EditLockEndpointsTests(SqlServerFixture server) : IDisposable
{
    private const string EditorPassword = "lock-editor-password";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    /// <summary>
    /// The administrator takes the lock; the second user reads it, is refused
    /// it with the holder named, and takes it once the administrator releases
    /// it — after which the administrator cannot release the second user's.
    /// </summary>
    [Fact]
    public async Task ALockHeldIsRefusedToAnotherUntilReleased()
    {
        var admin = api.CreateClient();
        var editor = await SignedInEditor();
        var report = await Open(admin);

        try
        {
            var taken = await Took(admin, report);
            Assert.NotNull(taken.UserId);
            Assert.Equal(taken.LockedAt + EditLockRules.Timeout, taken.ExpiresAt);

            var seen = (await editor.GetFromJsonAsync<EditLockResponse>(Lock(report), Json))!;
            Assert.Equal(taken.UserId, seen.UserId);
            Assert.False(string.IsNullOrWhiteSpace(seen.UserName));

            using (var refused = await editor.PostAsync(Lock(report), null))
            {
                Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
                Assert.Contains(seen.UserName!, await Detail(refused), StringComparison.Ordinal);
            }

            using (var notTheirs = await editor.DeleteAsync(Lock(report)))
            {
                Assert.Equal(HttpStatusCode.Conflict, notTheirs.StatusCode);
            }

            var again = await Took(admin, report);
            Assert.Equal(taken.UserId, again.UserId);

            using (var released = await admin.DeleteAsync(Lock(report)))
            {
                Assert.Equal(HttpStatusCode.NoContent, released.StatusCode);
            }

            var free = (await editor.GetFromJsonAsync<EditLockResponse>(Lock(report), Json))!;
            Assert.Null(free.UserId);

            var theirs = await Took(editor, report);
            Assert.NotEqual(taken.UserId, theirs.UserId);

            using (var adminRefused = await admin.DeleteAsync(Lock(report)))
            {
                Assert.Equal(HttpStatusCode.Conflict, adminRefused.StatusCode);
            }
        }
        finally
        {
            await Remove(report);
        }
    }

    /// <summary>
    /// A lock taken longer ago than the timeout is held by nobody: the second
    /// user reads it as free and takes it.
    /// </summary>
    [Fact]
    public async Task ALockPastItsTimeoutIsTakenByAnother()
    {
        var admin = api.CreateClient();
        var editor = await SignedInEditor();
        var report = await Open(admin);

        try
        {
            var taken = await Took(admin, report);

            await using (var context = server.NewContext())
            {
                var stale = DateTimeOffset.UtcNow - EditLockRules.Timeout - TimeSpan.FromMinutes(1);
                await context.PersonConditions
                    .Where(row => row.Id == report)
                    .ExecuteUpdateAsync(set => set.SetProperty(row => row.LockedAt, stale));
            }

            Assert.Null((await editor.GetFromJsonAsync<EditLockResponse>(Lock(report), Json))!.UserId);

            var theirs = await Took(editor, report);
            Assert.NotEqual(taken.UserId, theirs.UserId);
        }
        finally
        {
            await Remove(report);
        }
    }

    /// <summary>
    /// A deleted case cannot be locked, and a case nobody has is a 404 to every
    /// verb.
    /// </summary>
    [Fact]
    public async Task ADeletedCaseIsRefusedAndAMissingOneIsNotFound()
    {
        var admin = api.CreateClient();
        var report = await Open(admin);

        try
        {
            using (var deleted = await admin.DeleteAsync($"/api/person-conditions/{report}"))
            {
                deleted.EnsureSuccessStatusCode();
            }

            using var refused = await admin.PostAsync(Lock(report), null);
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            Assert.Contains("deleted", await Detail(refused), StringComparison.Ordinal);
        }
        finally
        {
            await Remove(report);
        }

        using var read = await admin.GetAsync(Lock(987654));
        using var take = await admin.PostAsync(Lock(987654), null);
        using var release = await admin.DeleteAsync(Lock(987654));
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, take.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, release.StatusCode);
    }

    private static string Lock(int report) => $"/api/person-conditions/{report}/lock";

    private static async Task<EditLockResponse> Took(HttpClient client, int report)
    {
        using var response = await client.PostAsync(Lock(report), null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<EditLockResponse>(Json))!;
    }

    private static async Task<string> Detail(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return problem.RootElement.GetProperty("detail").GetString()!;
    }

    /// <summary>Opens a case on a seeded person and Malaria, and answers its id.</summary>
    private static async Task<int> Open(HttpClient admin)
    {
        var persons = (await admin.GetFromJsonAsync<PagedResult<PersonResponse>>("/api/persons?pageSize=200", Json))!;
        var conditions = (await admin.GetFromJsonAsync<PagedResult<ConditionResponse>>(
            "/api/conditions?pageSize=200", Json))!;

        using var created = await admin.PostAsJsonAsync(
            "/api/person-conditions",
            new CreatePersonConditionRequest(
                persons.Items[0].Id,
                conditions.Items.Single(condition => condition.Name == "Malaria").Id),
            Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        return (await created.Content.ReadFromJsonAsync<PersonConditionResponse>(Json))!.Id;
    }

    private async Task Remove(int report)
    {
        await using var context = server.NewContext();

        await context.FormAssignments.Where(row => row.PersonConditionId == report).ExecuteDeleteAsync();
        await context.PersonConditions.Where(row => row.Id == report).ExecuteDeleteAsync();
    }

    /// <summary>
    /// A client signed in as a second user holding view_cases and edit_cases,
    /// created over the wire and found again on a later run.
    /// </summary>
    private async Task<HttpClient> SignedInEditor()
    {
        var admin = api.CreateClient();

        var roles = (await admin.GetFromJsonAsync<PagedResult<RoleResponse>>("/api/roles", Json))!;
        var role = roles.Items.SingleOrDefault(role => role.Name == "Case editor")
            ?? await Created<RoleResponse>(admin, "/api/roles",
                new SaveRoleRequest("Case editor", [Privileges.ViewCases, Privileges.EditCases]));

        var users = (await admin.GetFromJsonAsync<PagedResult<UserResponse>>("/api/users", Json))!;
        if (users.Items.All(user => user.UserName != "lock-editor"))
        {
            await Created<UserResponse>(admin, "/api/users",
                new SaveUserRequest("lock-editor", "Lock Editor", true, [role.Id], EditorPassword));
        }

        var editor = api.CreateAnonymousClient();
        await api.SignIn(editor, "lock-editor", EditorPassword);

        return editor;
    }

    private static async Task<T> Created<T>(HttpClient client, string url, object body)
    {
        using var response = await client.PostAsJsonAsync(url, body, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }
}
