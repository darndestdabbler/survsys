using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The Connecticut towns and the zip codes, against the real database: a town
/// added, then corrected by an upload, fills the next address saved with it,
/// and a zip code imported fills the county of an address outside
/// Connecticut — so a row an administrator corrects takes effect on the next
/// save, with no release between.
/// </summary>
/// <remarks>
/// The town and zip codes the tests add are their own, and they are deleted
/// with the persons the tests create, so the seeded rows are only read.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class PlaceEndpointsTests(SqlServerFixture server) : IDisposable
{
    private const string Town = "Itestville";

    private const string Zip = "00082";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task ATownCorrectedByUploadIsUsedByTheNextAddressSave()
    {
        await Clear();
        var client = api.CreateClient();
        var id = await Created(client, "Townsend");

        try
        {
            using (var added = await client.PostAsJsonAsync(
                "/api/ct-towns", new CtTownRow(Town, "09001", "09120"), Json))
            {
                Assert.Equal(HttpStatusCode.Created, added.StatusCode);
            }

            var csv = "town,county,planning_region\r\n"
                + $"{Town},09003,09110\r\n"
                + "Itestburg,09999,09110\r\n";

            var preview = await Import(client, "/api/ct-towns/csv", csv, confirm: false);

            Assert.Equal([new ReferenceImportRow(2, Town)], preview.Changed);
            Assert.Empty(preview.Added);
            Assert.Contains("09999 is not a code on the county list", Assert.Single(preview.Refused).Reason);
            Assert.Equal(new CtTownRow(Town, "09001", "09120"), await TownOnFile(client));

            await Import(client, "/api/ct-towns/csv", csv, confirm: true);
            Assert.Equal(new CtTownRow(Town, "09003", "09110"), await TownOnFile(client));

            var saved = await Put(client, id, new PersonAddressRequest(City: Town.ToLowerInvariant(), State: "CT"));

            Assert.Equal(("09003", "09110"), (saved.County, saved.CountyEquivalent));
        }
        finally
        {
            await Forget(id);
            await Clear();
        }
    }

    [Fact]
    public async Task AZipCodeImportedFillsTheCountyOfAnAddressOutsideConnecticut()
    {
        await Clear();
        var client = api.CreateClient();
        var id = await Created(client, "Zipperman");

        try
        {
            var csv = $"zip_code,county\r\n{Zip}-1234,SL\r\nnot-a-zip,SL\r\n";

            var preview = await Import(client, "/api/zip-codes/csv", csv, confirm: false);

            Assert.Equal([new ReferenceImportRow(2, Zip)], preview.Added);
            Assert.Equal(3, Assert.Single(preview.Refused).Line);
            Assert.DoesNotContain(await ZipCodes(client), row => row.Code == Zip);

            await Import(client, "/api/zip-codes/csv", csv, confirm: true);
            Assert.Contains(new ZipCodeRow(Zip, "SL"), await ZipCodes(client));

            var saved = await Put(client, id, new PersonAddressRequest(City: "Salt Lake City", State: "UT", PostalCode: Zip));

            Assert.Equal("SL", saved.County);
            Assert.Null(saved.CountyEquivalent);
        }
        finally
        {
            await Forget(id);
            await Clear();
        }
    }

    private static async Task<int> Created(HttpClient client, string lastName)
    {
        using var created = await client.PostAsJsonAsync("/api/persons", new CreatePersonRequest(lastName), Json);
        created.EnsureSuccessStatusCode();
        return (await created.Content.ReadFromJsonAsync<PersonResponse>(Json))!.Id;
    }

    /// <summary>Saves the person with the one address, and answers that address as saved.</summary>
    private static async Task<PersonAddressResponse> Put(HttpClient client, int id, PersonAddressRequest address)
    {
        using var response = await client.PutAsJsonAsync(
            $"/api/persons/{id}", new UpdatePersonRequest("Placed") { Addresses = [address] }, Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.Single((await response.Content.ReadFromJsonAsync<PersonDetailResponse>(Json))!.Addresses);
    }

    private static async Task<ReferenceImportResponse> Import(HttpClient client, string path, string csv, bool confirm)
    {
        using var posted = await client.PostAsync(
            $"{path}{(confirm ? "?confirm=true" : "")}", new StringContent(csv, Encoding.UTF8, "text/csv"));
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        return (await posted.Content.ReadFromJsonAsync<ReferenceImportResponse>(Json))!;
    }

    private static async Task<CtTownRow> TownOnFile(HttpClient client) =>
        Assert.Single((await client.GetFromJsonAsync<List<CtTownRow>>("/api/ct-towns", Json))!, row => row.Town == Town);

    private static async Task<List<ZipCodeRow>> ZipCodes(HttpClient client) =>
        (await client.GetFromJsonAsync<List<ZipCodeRow>>("/api/zip-codes", Json))!;

    private async Task Forget(int personId)
    {
        await using var context = server.NewContext();
        await context.Persons.Where(person => person.Id == personId).ExecuteDeleteAsync();
    }

    private async Task Clear()
    {
        await using var context = server.NewContext();
        await context.CtTowns.Where(town => town.Town == Town).ExecuteDeleteAsync();
        await context.ZipCodes.Where(zip => zip.Code == Zip).ExecuteDeleteAsync();
    }
}
