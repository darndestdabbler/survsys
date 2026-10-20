using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of <c>/api/vaccines</c>: the URLs it
/// asks, the catalogue and records it hands a screen, and the phrases its
/// failures start with.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s. What is this
/// client's own is small, which is the point of it being this small.
/// </remarks>
public class VaccinesClientTests
{
    private const string Json = "application/json";

    private const string Catalogue =
        """
        [
          { "id": 15, "code": "03", "description": "MMR", "sortOrder": 15, "active": true },
          { "id": 23, "code": "115", "description": "Tdap", "sortOrder": 23, "active": false }
        ]
        """;

    private const string OneVaccine =
        """
        { "id": 15, "code": "03", "description": "MMR", "sortOrder": 15, "active": true }
        """;

    private const string TwoRecords =
        """
        [
          { "id": 1, "vaccineId": 15, "vaccineDescription": "MMR", "administeredDate": "2026-03-01",
            "doseNumber": 2, "lotNumber": "ABC123", "expirationDate": "2027-03-01", "comment": "No reaction" },
          { "id": 2, "vaccineId": 23, "vaccineDescription": "Tdap", "administeredDate": null,
            "doseNumber": null, "lotNumber": null, "expirationDate": null, "comment": null }
        ]
        """;

    private const string OneRecord =
        """
        { "id": 1, "vaccineId": 15, "vaccineDescription": "MMR", "administeredDate": "2026-03-01",
          "doseNumber": 2, "lotNumber": "ABC123", "expirationDate": "2027-03-01", "comment": "No reaction" }
        """;

    [Fact]
    public async Task CatalogueAsksForTheVaccines()
    {
        Uri? asked = null;

        var client = new VaccinesClient(Transport(request =>
        {
            asked = request.RequestUri;
            return Answer(HttpStatusCode.OK, Catalogue);
        }));

        var catalogue = await client.Catalogue();

        Assert.Equal("http://api.example.test/api/vaccines", asked?.ToString());
        Assert.Equal([15, 23], catalogue.Value!.Select(vaccine => vaccine.Id));
        Assert.Equal("MMR", catalogue.Value[0].Description);
        Assert.False(catalogue.Value[1].Active);
    }

    [Fact]
    public async Task AddVaccinePostsToTheCatalogue()
    {
        HttpRequestMessage? sent = null;
        var client = new VaccinesClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.Created, OneVaccine);
        }));

        var added = await client.AddVaccine(new SaveVaccineRequest("03", "MMR", 15));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/vaccines", sent?.RequestUri?.ToString());
        Assert.Equal(15, added.Value!.Id);
    }

    [Fact]
    public async Task SaveVaccinePutsToTheVaccine()
    {
        HttpRequestMessage? sent = null;
        var client = new VaccinesClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, OneVaccine);
        }));

        var saved = await client.SaveVaccine(15, new SaveVaccineRequest("03", "MMR", 15, Active: false));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/vaccines/15", sent?.RequestUri?.ToString());
        Assert.True(saved.Succeeded);
    }

    [Fact]
    public async Task AVaccineSaveThatFailedCarriesTheVaccineSavePrefix()
    {
        var client = new VaccinesClient(Transport(_ => Answer(HttpStatusCode.Conflict, "{}")));

        var added = await client.AddVaccine(new SaveVaccineRequest("03"));

        Assert.StartsWith(VaccinesClient.VaccineSaveFailurePrefix, added.Failure);
    }

    [Fact]
    public async Task ExportAsksForTheCatalogueAsCsv()
    {
        Uri? asked = null;
        var client = new VaccinesClient(Transport(request =>
        {
            asked = request.RequestUri;
            return Answer(HttpStatusCode.OK, "code,description,sort_order,active\r\n", "text/csv");
        }));

        var exported = await client.Export();

        Assert.Equal("http://api.example.test/api/vaccines/csv", asked?.ToString());
        Assert.StartsWith("code,description", exported.Value);
    }

    [Theory]
    [InlineData(false, "http://api.example.test/api/vaccines/csv")]
    [InlineData(true, "http://api.example.test/api/vaccines/csv?confirm=true")]
    public async Task ImportPostsTheFileAndConfirmsOnlyWhenAsked(bool confirm, string expected)
    {
        HttpRequestMessage? sent = null;
        string? body = null;
        var client = new VaccinesClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
            return Answer(
                HttpStatusCode.OK,
                """{ "applied": false, "added": [{ "line": 2, "key": "03" }], "changed": [], "refused": [] }""");
        }));

        var imported = await client.Import("code\r\n03\r\n", confirm);

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal(expected, sent?.RequestUri?.ToString());
        Assert.Equal("code\r\n03\r\n", body);
        Assert.Equal("03", Assert.Single(imported.Value!.Added).Key);
    }

    [Fact]
    public async Task AnImportThatFailedCarriesTheImportPrefix()
    {
        var client = new VaccinesClient(Transport(_ => Answer(HttpStatusCode.InternalServerError, "{}")));

        var imported = await client.Import("code\r\n03\r\n", confirm: false);

        Assert.StartsWith(VaccinesClient.ImportFailurePrefix, imported.Failure);
    }

    [Fact]
    public async Task CatalogueFailureCarriesItsPrefix()
    {
        var client = new VaccinesClient(
            Transport(_ => Answer(HttpStatusCode.InternalServerError, "{}")));

        var catalogue = await client.Catalogue();

        Assert.False(catalogue.Succeeded);
        Assert.StartsWith(VaccinesClient.CatalogueFailurePrefix, catalogue.Failure);
    }

    [Fact]
    public async Task RecordsAsksForTheCasesVaccines()
    {
        Uri? asked = null;
        var client = new VaccinesClient(Transport(request =>
        {
            asked = request.RequestUri;
            return Answer(HttpStatusCode.OK, TwoRecords);
        }));

        var records = await client.Records(7);

        Assert.Equal("http://api.example.test/api/cases/7/vaccines", asked?.ToString());
        Assert.Equal([1, 2], records.Value!.Select(record => record.Id));
        Assert.Equal("MMR", records.Value[0].VaccineDescription);
    }

    [Fact]
    public async Task RecordsFailureCarriesItsPrefix()
    {
        var client = new VaccinesClient(Transport(_ => Answer(HttpStatusCode.NotFound, "{}")));

        var records = await client.Records(7);

        Assert.False(records.Succeeded);
        Assert.StartsWith(VaccinesClient.RecordsFailurePrefix, records.Failure);
    }

    [Fact]
    public async Task AddPostsToTheCase()
    {
        HttpRequestMessage? sent = null;
        var client = new VaccinesClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.Created, OneRecord);
        }));

        var added = await client.Add(7, new SaveVaccineRecordRequest(15, DoseNumber: 2));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/cases/7/vaccines", sent?.RequestUri?.ToString());
        Assert.Equal(1, added.Value!.Id);
    }

    [Fact]
    public async Task UpdatePutsToTheRecord()
    {
        HttpRequestMessage? sent = null;
        var client = new VaccinesClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, OneRecord);
        }));

        var saved = await client.Update(1, new SaveVaccineRecordRequest(15));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/cases/vaccines/1", sent?.RequestUri?.ToString());
        Assert.True(saved.Succeeded);
    }

    [Fact]
    public async Task DeleteDeletesTheRecord()
    {
        HttpRequestMessage? sent = null;
        var client = new VaccinesClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.NoContent, "");
        }));

        var why = await client.Delete(1);

        Assert.Equal(HttpMethod.Delete, sent?.Method);
        Assert.Equal("http://api.example.test/api/cases/vaccines/1", sent?.RequestUri?.ToString());
        Assert.Null(why);
    }

    [Fact]
    public async Task ASaveThatFailedCarriesTheSavePrefix()
    {
        var client = new VaccinesClient(Transport(_ => Answer(HttpStatusCode.BadRequest, "{}")));

        var added = await client.Add(7, new SaveVaccineRecordRequest(0));

        Assert.StartsWith(VaccinesClient.SaveFailurePrefix, added.Failure);
    }

    private static HttpClient Transport(Func<HttpRequestMessage, HttpResponseMessage> answer) =>
        new(new StubTransport(answer)) { BaseAddress = new Uri("http://api.example.test/") };

    private static HttpResponseMessage Answer(
        HttpStatusCode status,
        string body,
        string mediaType = Json) =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8)
            {
                Headers = { ContentType = new MediaTypeHeaderValue(mediaType) },
            },
        };

    private sealed class StubTransport(Func<HttpRequestMessage, HttpResponseMessage> answer)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(answer(request));
    }
}
