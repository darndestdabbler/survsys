using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The provider registry against the real database: a provider created, read,
/// found by the start of their last name and saved over; a provider type not
/// on its list refused; and a CSV file previewed without writing, then
/// confirmed, then exported.
/// </summary>
/// <remarks>
/// The providers the test adds are its own, every last name beginning
/// <see cref="Prefix"/>, and are deleted before and after.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class ProviderEndpointsTests(SqlServerFixture server) : IDisposable
{
    private const string Prefix = "Itestprov";

    /// <summary>The seeded clinician, the one provider type the legacy names.</summary>
    private const int Clinician = 1175;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task AProviderIsCreatedFoundByLastNameAndSavedOver()
    {
        var client = api.CreateClient();
        await Clear();

        try
        {
            using var created = await client.PostAsJsonAsync(
                "/api/providers",
                new SaveProviderRequest($"{Prefix}Lovelace", " Ada ", ProviderTypeId: Clinician, Email: "ada@example.org"),
                Json);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var ada = (await created.Content.ReadFromJsonAsync<ProviderResponse>(Json))!;
            Assert.Equal(
                new ProviderResponse(ada.Id, Clinician, "Ada", null, $"{Prefix}Lovelace", null, "ada@example.org", true),
                ada);

            using var other = await client.PostAsJsonAsync(
                "/api/providers", new SaveProviderRequest($"{Prefix}Hopper", "Grace"), Json);
            Assert.Equal(HttpStatusCode.Created, other.StatusCode);

            var found = (await client.GetFromJsonAsync<PagedResult<ProviderResponse>>(
                $"/api/providers?lastName={Prefix}Lov", Json))!;
            Assert.Equal(1, found.TotalCount);
            Assert.Equal(ada.Id, Assert.Single(found.Items).Id);

            using var saved = await client.PutAsJsonAsync(
                $"/api/providers/{ada.Id}", Request(ada) with { Active = false, Phone = "860-555-0123" }, Json);
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

            var read = await Find(client, ada.Id);
            Assert.False(read.Active);
            Assert.Equal("860-555-0123", read.Phone);
        }
        finally
        {
            await Clear();
        }
    }

    [Fact]
    public async Task AProviderTypeNotOnItsListIsRefused()
    {
        var client = api.CreateClient();
        var race = await ACodeOn(CodeLists.Race);

        using var created = await client.PostAsJsonAsync(
            "/api/providers", new SaveProviderRequest($"{Prefix}Typed", ProviderTypeId: race), Json);

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Contains($"providerTypeId {race} is not a provider type", await created.Content.ReadAsStringAsync());

        using var missing = await client.GetAsync("/api/providers/987654321");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task AProviderFileIsPreviewedThenImportedAndExported()
    {
        var client = api.CreateClient();
        await Clear();

        try
        {
            using var created = await client.PostAsJsonAsync(
                "/api/providers", new SaveProviderRequest($"{Prefix}A"), Json);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var a = (await created.Content.ReadFromJsonAsync<ProviderResponse>(Json))!;

            var csv = "id,provider_type_id,first_name,middle_name,last_name,phone,email,active\r\n"
                + $",{Clinician},Bea,,{Prefix}B,860-555-0100,bea@example.org,\r\n"
                + $"{a.Id},,,,{Prefix}A renamed,,,no\r\n"
                + $",987654321,,,{Prefix}C,,,yes\r\n"
                + $"987654321,,,,{Prefix}D,,,yes\r\n";

            var preview = await Import(client, csv, confirm: false);

            Assert.Equal([new ReferenceImportRow(2, $"{Prefix}B")], preview.Added);
            Assert.Equal([new ReferenceImportRow(3, $"{a.Id}")], preview.Changed);
            Assert.Equal([4, 5], preview.Refused.Select(problem => problem.Line));
            Assert.Contains("providerTypeId 987654321 is not a provider type", preview.Refused[0].Reason);
            Assert.Contains("is not a provider on file", preview.Refused[1].Reason);
            Assert.Equal($"{Prefix}A", (await Find(client, a.Id)).LastName);

            var confirmed = await Import(client, csv, confirm: true);
            Assert.True(confirmed.Applied);

            var renamed = await Find(client, a.Id);
            Assert.Equal($"{Prefix}A renamed", renamed.LastName);
            Assert.False(renamed.Active);

            var exported = await client.GetStringAsync("/api/providers/csv");
            Assert.Contains($",{Clinician},Bea,,{Prefix}B,860-555-0100,bea@example.org,yes\r\n", exported);
            Assert.Contains($"{a.Id},,,,{Prefix}A renamed,,,no\r\n", exported);
        }
        finally
        {
            await Clear();
        }
    }

    private static SaveProviderRequest Request(ProviderResponse provider) =>
        new(provider.LastName, provider.FirstName, provider.MiddleName, provider.ProviderTypeId,
            provider.Phone, provider.Email, provider.Active);

    private static async Task<ReferenceImportResponse> Import(HttpClient client, string csv, bool confirm)
    {
        using var posted = await client.PostAsync(
            $"/api/providers/csv{(confirm ? "?confirm=true" : "")}", new StringContent(csv, Encoding.UTF8, "text/csv"));
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        return (await posted.Content.ReadFromJsonAsync<ReferenceImportResponse>(Json))!;
    }

    private static async Task<ProviderResponse> Find(HttpClient client, int id) =>
        (await client.GetFromJsonAsync<ProviderResponse>($"/api/providers/{id}", Json))!;

    private async Task<int> ACodeOn(string list)
    {
        await using var context = server.NewContext();
        return await context.CodeValues.Where(code => code.List == list).Select(code => code.Id).FirstAsync();
    }

    private async Task Clear()
    {
        await using var context = server.NewContext();
        await context.Providers.Where(provider => provider.LastName.StartsWith(Prefix)).ExecuteDeleteAsync();
    }
}
