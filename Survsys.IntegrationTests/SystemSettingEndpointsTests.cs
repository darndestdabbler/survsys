using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Survsys.Contracts;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// <c>Survsys.Api/SystemSettingEndpoints.cs</c> against the running host: a
/// setting is added by saving it under a new name, saved over under the same
/// name, listed, and deleted; deleting one that is not there is a 404, and a
/// setting with no value is a 400.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class SystemSettingEndpointsTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task ASettingIsAddedSavedOverListedAndDeleted()
    {
        var client = api.CreateClient();
        var name = "itest_" + Guid.NewGuid().ToString("N");

        using var added = await client.PutAsJsonAsync(
            $"/api/system-settings/{name}", new SaveSystemSettingRequest("one", "An itest setting"), Json);
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);

        using var saved = await client.PutAsJsonAsync(
            $"/api/system-settings/{name}", new SaveSystemSettingRequest("two"), Json);
        Assert.Equal(new SystemSettingResponse(name, "two", null),
            await saved.Content.ReadFromJsonAsync<SystemSettingResponse>(Json));

        var listed = (await client.GetFromJsonAsync<IReadOnlyList<SystemSettingResponse>>("/api/system-settings", Json))!;
        Assert.Equal(new SystemSettingResponse(name, "two", null), Assert.Single(listed, setting => setting.Name == name));

        using var deleted = await client.DeleteAsync($"/api/system-settings/{name}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using var again = await client.DeleteAsync($"/api/system-settings/{name}");
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task ASettingWithNoValueIsA400()
    {
        using var response = await api.CreateClient().PutAsJsonAsync(
            "/api/system-settings/itest_no_value", new { description = "no value" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
