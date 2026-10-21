using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The facilities' CSV import and export, against the real database: a file
/// previewed writes nothing; confirmed, it adds the row whose id is blank and
/// saves over the row whose id is on file, and refuses a row naming an agency
/// or an id not on file; and the export lists what the import saved.
/// </summary>
/// <remarks>
/// The facilities the test adds are its own, every name beginning
/// <see cref="Prefix"/>, and are deleted before and after, so the seeded
/// facilities are only read.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class FacilityEndpointsTests(SqlServerFixture server) : IDisposable
{
    private const string Prefix = "Itest Lab";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task AFacilityFileIsPreviewedThenImportedAndExported()
    {
        var client = api.CreateClient();
        await Clear();

        try
        {
            var agency = await AnAgency();

            using var created = await client.PostAsJsonAsync(
                "/api/facilities", new SaveFacilityRequest($"{Prefix} A", agency, "ITA"), Json);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var a = (await created.Content.ReadFromJsonAsync<FacilityResponse>(Json))!;

            var csv = "id,name,short_name,agency_id,street,city,state,zip,phone\r\n"
                + $",{Prefix} B,ITB,{agency},1 Main St,Hartford,CT,06101-1234,860-555-0100\r\n"
                + $"{a.Id},{Prefix} A renamed,ITA,{agency},,,,,\r\n"
                + $",{Prefix} C,,987654321,,,,,\r\n"
                + $"987654321,{Prefix} D,,{agency},,,,,\r\n";

            var preview = await Import(client, csv, confirm: false);

            Assert.Equal([new ReferenceImportRow(2, $"{Prefix} B")], preview.Added);
            Assert.Equal([new ReferenceImportRow(3, $"{a.Id}")], preview.Changed);
            Assert.Equal([4, 5], preview.Refused.Select(problem => problem.Line));
            Assert.Contains("agencyId 987654321 is not an agency", preview.Refused[0].Reason);
            Assert.Contains("is not a facility on file", preview.Refused[1].Reason);
            Assert.Equal($"{Prefix} A", (await Find(client, a.Id)).Name);

            var confirmed = await Import(client, csv, confirm: true);
            Assert.True(confirmed.Applied);

            Assert.Equal($"{Prefix} A renamed", (await Find(client, a.Id)).Name);

            var exported = await client.GetStringAsync("/api/facilities/csv");
            Assert.Contains($",{Prefix} B,ITB,{agency},1 Main St,Hartford,CT,06101,860-555-0100\r\n", exported);
            Assert.Contains($"{a.Id},{Prefix} A renamed,ITA,{agency},,,,,\r\n", exported);
        }
        finally
        {
            await Clear();
        }
    }

    private static async Task<ReferenceImportResponse> Import(HttpClient client, string csv, bool confirm)
    {
        using var posted = await client.PostAsync(
            $"/api/facilities/csv{(confirm ? "?confirm=true" : "")}", new StringContent(csv, Encoding.UTF8, "text/csv"));
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        return (await posted.Content.ReadFromJsonAsync<ReferenceImportResponse>(Json))!;
    }

    private static async Task<FacilityResponse> Find(HttpClient client, int id) =>
        (await client.GetFromJsonAsync<FacilityResponse>($"/api/facilities/{id}", Json))!;

    private async Task<int> AnAgency()
    {
        await using var context = server.NewContext();
        return await context.Agencies.OrderBy(agency => agency.Id).Select(agency => agency.Id).FirstAsync();
    }

    private async Task Clear()
    {
        await using var context = server.NewContext();
        await context.Facilities.Where(facility => facility.Name.StartsWith(Prefix)).ExecuteDeleteAsync();
    }
}
