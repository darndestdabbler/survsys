using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// An agency's jurisdiction zip codes imported from CSV, against the real
/// database: the preview adds no zip, the confirmed import adds the zips the
/// agency did not hold and keeps the one the file left out, and a zip it added
/// then resolves to the agency.
/// </summary>
/// <remarks>
/// The agency and zip codes are the test's own, deleted before and after it,
/// so the sixteen seeded agencies are only read.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class AgencyEndpointsTests(SqlServerFixture server) : IDisposable
{
    private const string Name = "ITEST Import Agency";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task AnAgencysZipCodesAreImportedAfterAPreviewAndThenResolveToIt()
    {
        await Clear();
        var client = api.CreateClient();

        try
        {
            using var created = await client.PostAsJsonAsync(
                "/api/agencies", new SaveAgencyRequest(Name, ZipCodes: ["00083", "00085"]), Json);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var id = (await created.Content.ReadFromJsonAsync<AgencyDetailResponse>(Json))!.Id;

            const string csv = "zip_code\r\n00083\r\n00084-1234\r\nabc\r\n";

            var preview = await Import(client, id, csv, confirm: false);

            Assert.Equal([new ReferenceImportRow(3, "00084")], preview.Added);
            Assert.Empty(preview.Changed);
            Assert.Equal(4, Assert.Single(preview.Refused).Line);
            using (var unmapped = await client.GetAsync("/api/agencies/by-zip/00084"))
            {
                Assert.Equal(HttpStatusCode.NotFound, unmapped.StatusCode);
            }

            var confirmed = await Import(client, id, csv, confirm: true);

            Assert.True(confirmed.Applied);
            var resolved = (await client.GetFromJsonAsync<AgencyResponse>("/api/agencies/by-zip/00084", Json))!;
            Assert.Equal(id, resolved.Id);
            var agency = (await client.GetFromJsonAsync<AgencyDetailResponse>($"/api/agencies/{id}", Json))!;
            Assert.Equal(["00083", "00084", "00085"], agency.ZipCodes);
            Assert.Equal("zip_code\r\n00083\r\n00084\r\n00085\r\n", await client.GetStringAsync($"/api/agencies/{id}/zip-codes/csv"));
        }
        finally
        {
            await Clear();
        }
    }

    [Fact]
    public async Task AnImportForNoSuchAgencyIsNotFound()
    {
        using var posted = await api.CreateClient().PostAsync(
            "/api/agencies/99999/zip-codes/csv", new StringContent("zip_code\r\n00083\r\n", Encoding.UTF8, "text/csv"));

        Assert.Equal(HttpStatusCode.NotFound, posted.StatusCode);
    }

    private static async Task<ReferenceImportResponse> Import(HttpClient client, int id, string csv, bool confirm)
    {
        using var posted = await client.PostAsync(
            $"/api/agencies/{id}/zip-codes/csv{(confirm ? "?confirm=true" : "")}",
            new StringContent(csv, Encoding.UTF8, "text/csv"));
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        return (await posted.Content.ReadFromJsonAsync<ReferenceImportResponse>(Json))!;
    }

    private async Task Clear()
    {
        await using var context = server.NewContext();
        var ids = await context.Agencies.Where(agency => agency.Name == Name).Select(agency => agency.Id).ToListAsync();
        await context.ZipCodeAgencies.Where(mapping => ids.Contains(mapping.AgencyId)).ExecuteDeleteAsync();
        await context.Agencies.Where(agency => ids.Contains(agency.Id)).ExecuteDeleteAsync();
    }
}
