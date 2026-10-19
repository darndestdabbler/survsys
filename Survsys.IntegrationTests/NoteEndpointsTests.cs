using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// A note's privacy against the real database: a private note is recorded
/// under its author and listed only to them, where a public note is listed to
/// everyone.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class NoteEndpointsTests(SqlServerFixture server) : IDisposable
{
    private const string ReaderPassword = "note-reader-password";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task APrivateNoteIsListedToItsAuthorAloneAndAPublicNoteToBoth()
    {
        var admin = api.CreateClient();
        var reader = await SignedInReader();
        var report = await Open(admin);

        try
        {
            var adminId = (await admin.GetFromJsonAsync<CurrentUserResponse>("/api/me", Json))!.Id;

            var shared = await Recorded(admin, report,
                new CreateNoteRequest("Called the clinic", "clinical"));
            var mine = await Recorded(admin, report,
                new CreateNoteRequest("Patient seemed evasive", "clinical", UserId: 987654, Private: true));

            Assert.True(mine.Private);
            Assert.Equal(adminId, mine.UserId);
            Assert.False(shared.Private);

            var authorSees = await Notes(admin, report);
            Assert.Equal([mine.Id, shared.Id], authorSees.Items.Select(note => note.Id));
            Assert.Equal(2, authorSees.TotalCount);

            var otherSees = await Notes(reader, report);
            Assert.Equal([shared.Id], otherSees.Items.Select(note => note.Id));
            Assert.Equal(1, otherSees.TotalCount);
        }
        finally
        {
            await using var context = server.NewContext();
            await context.Notes.Where(note => note.PersonConditionId == report).ExecuteDeleteAsync();
            await context.FormAssignments.Where(row => row.PersonConditionId == report).ExecuteDeleteAsync();
            await context.PersonConditions.Where(row => row.Id == report).ExecuteDeleteAsync();
        }
    }

    private static async Task<NoteResponse> Recorded(HttpClient client, int report, CreateNoteRequest request)
    {
        using var response = await client.PostAsJsonAsync($"/api/person-conditions/{report}/notes", request, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<NoteResponse>(Json))!;
    }

    private static async Task<PagedResult<NoteResponse>> Notes(HttpClient client, int report) =>
        (await client.GetFromJsonAsync<PagedResult<NoteResponse>>($"/api/person-conditions/{report}/notes", Json))!;

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

    /// <summary>
    /// A client signed in as a second user holding view_cases, created over the
    /// wire and found again on a later run.
    /// </summary>
    private async Task<HttpClient> SignedInReader()
    {
        var admin = api.CreateClient();

        var roles = (await admin.GetFromJsonAsync<PagedResult<RoleResponse>>("/api/roles", Json))!;
        var role = roles.Items.SingleOrDefault(role => role.Name == "Case viewer")
            ?? await Created<RoleResponse>(admin, "/api/roles", new SaveRoleRequest("Case viewer", [Privileges.ViewCases]));

        var users = (await admin.GetFromJsonAsync<PagedResult<UserResponse>>("/api/users", Json))!;
        if (users.Items.All(user => user.UserName != "note-reader"))
        {
            await Created<UserResponse>(admin, "/api/users",
                new SaveUserRequest("note-reader", "Note Reader", true, [role.Id], ReaderPassword));
        }

        var reader = api.CreateAnonymousClient();
        await api.SignIn(reader, "note-reader", ReaderPassword);

        return reader;
    }

    private static async Task<T> Created<T>(HttpClient client, string url, object body)
    {
        using var response = await client.PostAsJsonAsync(url, body, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }
}
