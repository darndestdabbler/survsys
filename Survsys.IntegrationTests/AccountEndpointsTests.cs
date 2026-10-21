using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// Signing in against the real database: a user locked out after five wrong
/// passwords in a row, and a right password resetting the count.
/// </summary>
/// <remarks>
/// Each test signs in as a user of its own, so a lockout it causes cannot
/// refuse another test's sign-in.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class AccountEndpointsTests(SqlServerFixture server) : IDisposable
{
    private const string Password = "lockout-user-password";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    /// <summary>
    /// Five wrong passwords lock the user out: the right one is then refused
    /// in the same words, until the lockout has passed.
    /// </summary>
    [Fact]
    public async Task FiveWrongPasswordsLockTheUserOutUntilTheLockoutPasses()
    {
        var userId = await User("lockout-user");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, await SignIn("lockout-user", "wrong"));
        }

        Assert.Equal(HttpStatusCode.Unauthorized, await SignIn("lockout-user", Password));

        await using (var context = server.NewContext())
        {
            await context.Users
                .Where(user => user.Id == userId)
                .ExecuteUpdateAsync(set => set.SetProperty(user => user.LockedOutUntil, DateTimeOffset.UtcNow.AddMinutes(-1)));
        }

        Assert.Equal(HttpStatusCode.OK, await SignIn("lockout-user", Password));
    }

    /// <summary>
    /// Four wrong passwords, a right one, and four more do not lock the user
    /// out, because the right one began the count again.
    /// </summary>
    [Fact]
    public async Task ARightPasswordResetsTheCount()
    {
        await User("lockout-reset-user");

        for (var attempt = 0; attempt < 4; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, await SignIn("lockout-reset-user", "wrong"));
        }

        Assert.Equal(HttpStatusCode.OK, await SignIn("lockout-reset-user", Password));

        for (var attempt = 0; attempt < 4; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, await SignIn("lockout-reset-user", "wrong"));
        }

        Assert.Equal(HttpStatusCode.OK, await SignIn("lockout-reset-user", Password));
    }

    private async Task<HttpStatusCode> SignIn(string userName, string password)
    {
        using var client = api.CreateAnonymousClient();
        using var response = await client.PostAsJsonAsync("/api/sign-in", new SignInRequest(userName, password), Json);

        return response.StatusCode;
    }

    /// <summary>
    /// The user named, with this class's password and no failures or lockout,
    /// created over the wire on first use; answers their id.
    /// </summary>
    private async Task<int> User(string userName)
    {
        var admin = api.CreateClient();
        var users = (await admin.GetFromJsonAsync<PagedResult<UserResponse>>("/api/users", Json))!;

        if (users.Items.SingleOrDefault(user => user.UserName == userName) is not { } existing)
        {
            using var created = await admin.PostAsJsonAsync(
                "/api/users", new SaveUserRequest(userName, userName, true, [], Password), Json);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            existing = (await created.Content.ReadFromJsonAsync<UserResponse>(Json))!;
        }

        await using var context = server.NewContext();
        await context.Users
            .Where(user => user.Id == existing.Id)
            .ExecuteUpdateAsync(set => set
                .SetProperty(user => user.FailedSignInCount, 0)
                .SetProperty(user => user.LockedOutUntil, (DateTimeOffset?)null));

        return existing.Id;
    }
}
