using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// Signing in against the real database: a user locked out after five wrong
/// passwords in a row, in the words of any other refusal, a right password
/// resetting the count, and a token outliving its host where the data
/// protection keys are kept in a folder.
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

    /// <summary>
    /// Wrong passwords sent all at once are each counted, so they lock the
    /// user out as surely as wrong passwords sent one after another; and those
    /// past the fifth, landing once the lockout has begun, are not counted
    /// toward the next, so when it has passed the count starts again from none.
    /// </summary>
    [Fact]
    public async Task WrongPasswordsSentTogetherLockTheUserOutAndNoneCountsTowardTheNext()
    {
        var userId = await User("lockout-parallel-user");

        var attempts = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => SignIn("lockout-parallel-user", "wrong")));
        Assert.All(attempts, status => Assert.Equal(HttpStatusCode.Unauthorized, status));

        Assert.Equal(HttpStatusCode.Unauthorized, await SignIn("lockout-parallel-user", Password));

        await using (var context = server.NewContext())
        {
            Assert.Equal(0, (await context.Users.SingleAsync(user => user.Id == userId)).FailedSignInCount);
            await context.Users
                .Where(user => user.Id == userId)
                .ExecuteUpdateAsync(set => set.SetProperty(user => user.LockedOutUntil, DateTimeOffset.UtcNow.AddMinutes(-1)));
        }

        Assert.Equal(HttpStatusCode.Unauthorized, await SignIn("lockout-parallel-user", "wrong"));
        Assert.Equal(HttpStatusCode.OK, await SignIn("lockout-parallel-user", Password));
    }

    /// <summary>
    /// A locked-out user giving the right password is refused in the very
    /// words an unknown user name and a wrong password are, so the refusal
    /// says nothing of who exists or who is locked out.
    /// </summary>
    [Fact]
    public async Task ALockedOutUserIsRefusedInTheWordsOfAnyOtherRefusal()
    {
        await User("lockout-words-user");
        var wrong = await Refusal("lockout-words-user", "wrong");

        for (var attempt = 1; attempt < 5; attempt++)
        {
            await SignIn("lockout-words-user", "wrong");
        }

        var locked = await Refusal("lockout-words-user", Password);
        var unknown = await Refusal("no-such-user-anywhere", Password);

        Assert.Equal(wrong, locked);
        Assert.Equal(wrong, unknown);
    }

    /// <summary>
    /// With a key folder configured, a token one API host issued is accepted
    /// by another host started after the first has gone, as a second server or
    /// a recycled app pool would be; and the keys are in that folder.
    /// </summary>
    [Fact]
    public async Task ATokenOutlivesItsHostWhereTheKeysAreKeptInAFolder()
    {
        const string keysVariable = "Survsys__DataProtectionKeysPath";
        var keys = Directory.CreateTempSubdirectory("survsys-keys-");
        Environment.SetEnvironmentVariable(keysVariable, keys.FullName);

        try
        {
            await User("key-sharing-user");
            System.Net.Http.Headers.AuthenticationHeaderValue token;

            using (var first = new SurvsysApi(server.ConnectionString))
            {
                using var client = first.CreateAnonymousClient();
                await first.SignIn(client, "key-sharing-user", Password);
                token = client.DefaultRequestHeaders.Authorization!;
            }

            Assert.NotEmpty(keys.GetFiles("*.xml"));

            using var second = new SurvsysApi(server.ConnectionString);
            using var later = second.CreateAnonymousClient();
            later.DefaultRequestHeaders.Authorization = token;

            using var me = await later.GetAsync("/api/me");
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable(keysVariable, null);
            keys.Delete(recursive: true);
        }
    }

    private async Task<HttpStatusCode> SignIn(string userName, string password)
    {
        using var client = api.CreateAnonymousClient();
        using var response = await client.PostAsJsonAsync("/api/sign-in", new SignInRequest(userName, password), Json);

        return response.StatusCode;
    }

    /// <summary>The status, title and detail a refused sign-in answers with.</summary>
    private async Task<(HttpStatusCode Status, string? Title, string? Detail)> Refusal(string userName, string password)
    {
        using var client = api.CreateAnonymousClient();
        using var response = await client.PostAsJsonAsync("/api/sign-in", new SignInRequest(userName, password), Json);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return (
            response.StatusCode,
            problem.RootElement.GetProperty("title").GetString(),
            problem.RootElement.GetProperty("detail").GetString());
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
