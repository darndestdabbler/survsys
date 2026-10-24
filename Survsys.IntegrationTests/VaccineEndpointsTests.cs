using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The vaccine endpoints, against the real database: that the seeded catalogue
/// is read, that a record is added to a case and listed, edited and removed,
/// that a dose below one and a vaccine that does not exist are refused, and that
/// an unknown case has no records; and that the catalogue is added to, edited,
/// retired — a record naming a retired vaccine still showing it — imported
/// from CSV after a preview, and exported.
/// </summary>
/// <remarks>
/// Driven through the endpoints. Each test opens its own case and deletes it in
/// a finally, which cascades to the case's vaccine records, so a test leaves no
/// case or record behind; the seeded vaccine catalogue, persons and conditions
/// are left as they are, since the suite shares the one database and nothing here
/// changes them.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class VaccineEndpointsTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task TheCatalogueIsRead()
    {
        using var response = await api.CreateClient().GetAsync("/api/vaccines");
        response.EnsureSuccessStatusCode();

        var catalogue = (await response.Content
            .ReadFromJsonAsync<IReadOnlyList<VaccineResponse>>(Json))!;

        Assert.Contains(catalogue, vaccine => vaccine.Code == "03" && vaccine.Description == "MMR");
    }

    [Fact]
    public async Task ARecordIsAddedAndListed()
    {
        int caseId = 0;

        try
        {
            caseId = await CreateCase("Rodriguez, Maria", "Malaria");
            var vaccine = await VaccineId("03");

            var created = await Add(caseId, new SaveVaccineRecordRequest(
                vaccine,
                new DateOnly(2026, 3, 1),
                DoseNumber: 2,
                LotNumber: "ABC123",
                ExpirationDate: new DateOnly(2027, 3, 1),
                Comment: "No reaction",
                Manufacturer: "MSD",
                InformationSource: "PHC1435"));

            Assert.Equal(vaccine, created.VaccineId);
            Assert.Equal("MMR", created.VaccineDescription);
            Assert.Equal(2, created.DoseNumber);

            using var list = await api.CreateClient().GetAsync($"/api/cases/{caseId}/vaccines");
            list.EnsureSuccessStatusCode();
            var records = (await list.Content
                .ReadFromJsonAsync<IReadOnlyList<VaccineRecordResponse>>(Json))!;

            var read = Assert.Single(records);
            Assert.Equal("MMR", read.VaccineDescription);
            Assert.Equal("ABC123", read.LotNumber);
            Assert.Equal("No reaction", read.Comment);
            Assert.Equal("MSD", read.Manufacturer);
            Assert.Equal("PHC1435", read.InformationSource);
        }
        finally
        {
            await DeleteCase(caseId);
        }
    }

    [Fact]
    public async Task ARecordIsEditedAndRemoved()
    {
        int caseId = 0;

        try
        {
            caseId = await CreateCase("Rodriguez, Maria", "Malaria");
            var mmr = await VaccineId("03");
            var tdap = await VaccineId("115");

            var created = await Add(caseId, new SaveVaccineRecordRequest(mmr, DoseNumber: 1));

            using var edit = await api.CreateClient().PutAsJsonAsync(
                $"/api/cases/vaccines/{created.Id}",
                new SaveVaccineRecordRequest(tdap, DoseNumber: 3, Comment: "Booster"),
                Json);
            Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
            var saved = (await edit.Content.ReadFromJsonAsync<VaccineRecordResponse>(Json))!;
            Assert.Equal(tdap, saved.VaccineId);
            Assert.Equal("Tdap", saved.VaccineDescription);
            Assert.Equal(3, saved.DoseNumber);

            using var removed = await api.CreateClient().DeleteAsync($"/api/cases/vaccines/{created.Id}");
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

            using var list = await api.CreateClient().GetAsync($"/api/cases/{caseId}/vaccines");
            list.EnsureSuccessStatusCode();
            var records = (await list.Content
                .ReadFromJsonAsync<IReadOnlyList<VaccineRecordResponse>>(Json))!;
            Assert.Empty(records);
        }
        finally
        {
            await DeleteCase(caseId);
        }
    }

    [Fact]
    public async Task ARecordWithADoseBelowOneIsRefused()
    {
        int caseId = 0;

        try
        {
            caseId = await CreateCase("Rodriguez, Maria", "Malaria");
            var vaccine = await VaccineId("03");

            using var response = await api.CreateClient().PostAsJsonAsync(
                $"/api/cases/{caseId}/vaccines",
                new SaveVaccineRecordRequest(vaccine, DoseNumber: 0),
                Json);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            await DeleteCase(caseId);
        }
    }

    [Fact]
    public async Task ARecordForAVaccineThatDoesNotExistIsRefused()
    {
        int caseId = 0;

        try
        {
            caseId = await CreateCase("Rodriguez, Maria", "Malaria");

            using var response = await api.CreateClient().PostAsJsonAsync(
                $"/api/cases/{caseId}/vaccines",
                new SaveVaccineRecordRequest(987654321),
                Json);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            await DeleteCase(caseId);
        }
    }

    [Fact]
    public async Task AnUnknownCasesVaccinesAreNotFound()
    {
        using var response = await api.CreateClient().GetAsync("/api/cases/987654321/vaccines");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AVaccineIsAddedEditedAndRetiredAndARecordStillShowsIt()
    {
        int caseId = 0;
        var client = api.CreateClient();
        await ClearCatalogueRows();

        try
        {
            using var created = await client.PostAsJsonAsync(
                "/api/vaccines", new SaveVaccineRequest("ITEST1", "Itest vaccine", 90), Json);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var vaccine = (await created.Content.ReadFromJsonAsync<VaccineResponse>(Json))!;
            Assert.True(vaccine.Active);

            using (var taken = await client.PutAsJsonAsync(
                $"/api/vaccines/{vaccine.Id}", new SaveVaccineRequest("03", "Itest vaccine", 90), Json))
            {
                Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
            }

            caseId = await CreateCase("Rodriguez, Maria", "Malaria");
            await Add(caseId, new SaveVaccineRecordRequest(vaccine.Id, DoseNumber: 1));

            using (var retired = await client.PutAsJsonAsync(
                $"/api/vaccines/{vaccine.Id}", new SaveVaccineRequest("ITEST1", "Itest vaccine, retired", 90, Active: false), Json))
            {
                Assert.Equal(HttpStatusCode.OK, retired.StatusCode);
            }

            var catalogue = (await client.GetFromJsonAsync<List<VaccineResponse>>("/api/vaccines", Json))!;
            Assert.Contains(new VaccineResponse(vaccine.Id, "ITEST1", "Itest vaccine, retired", 90, false), catalogue);

            var records = (await client.GetFromJsonAsync<List<VaccineRecordResponse>>(
                $"/api/cases/{caseId}/vaccines", Json))!;
            Assert.Equal("Itest vaccine, retired", Assert.Single(records).VaccineDescription);
        }
        finally
        {
            await DeleteCase(caseId);
            await ClearCatalogueRows();
        }
    }

    [Fact]
    public async Task AVaccineFileIsPreviewedThenImportedAndExported()
    {
        var client = api.CreateClient();
        await ClearCatalogueRows();

        try
        {
            var csv = "code,description,sort_order,active\r\n"
                + "ITEST2,Itest two,91,yes\r\n"
                + "03,MMR,15,yes\r\n"
                + ",Nameless,1,yes\r\n";

            var preview = await Import(client, csv, confirm: false);

            Assert.Equal([new ReferenceImportRow(2, "ITEST2")], preview.Added);
            Assert.Empty(preview.Changed);
            var refused = Assert.Single(preview.Refused);
            Assert.Equal(4, refused.Line);
            Assert.Contains("code is required", refused.Reason);
            Assert.DoesNotContain(
                (await client.GetFromJsonAsync<List<VaccineResponse>>("/api/vaccines", Json))!,
                row => row.Code == "ITEST2");

            await Import(client, csv, confirm: true);

            var exported = await client.GetStringAsync("/api/vaccines/csv");
            Assert.StartsWith("code,description,sort_order,active\r\n", exported);
            Assert.Contains("ITEST2,Itest two,91,yes\r\n", exported);
            Assert.Contains("03,MMR,15,yes\r\n", exported);

            var again = await Import(client, exported, confirm: false);
            Assert.Empty(again.Added);
            Assert.Empty(again.Changed);
            Assert.Empty(again.Refused);
        }
        finally
        {
            await ClearCatalogueRows();
        }
    }

    private static async Task<ReferenceImportResponse> Import(HttpClient client, string csv, bool confirm)
    {
        using var posted = await client.PostAsync(
            $"/api/vaccines/csv{(confirm ? "?confirm=true" : "")}", new StringContent(csv, Encoding.UTF8, "text/csv"));
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        return (await posted.Content.ReadFromJsonAsync<ReferenceImportResponse>(Json))!;
    }

    /// <summary>Deletes the vaccines these tests add, all of whose codes begin ITEST.</summary>
    private async Task ClearCatalogueRows()
    {
        await using var context = server.NewContext();
        await context.Vaccines.Where(vaccine => vaccine.Code.StartsWith("ITEST")).ExecuteDeleteAsync();
    }

    private async Task<VaccineRecordResponse> Add(int personConditionId, SaveVaccineRecordRequest request)
    {
        using var response = await api.CreateClient()
            .PostAsJsonAsync($"/api/cases/{personConditionId}/vaccines", request, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<VaccineRecordResponse>(Json))!;
    }

    private async Task<int> VaccineId(string code)
    {
        using var response = await api.CreateClient().GetAsync("/api/vaccines");
        response.EnsureSuccessStatusCode();

        var catalogue = (await response.Content
            .ReadFromJsonAsync<IReadOnlyList<VaccineResponse>>(Json))!;

        return catalogue.Single(vaccine => vaccine.Code == code).Id;
    }

    private async Task<int> CreateCase(string personNamed, string conditionNamed)
    {
        var person = await PersonId(personNamed);
        var condition = await ConditionId(conditionNamed);

        using var response = await api.CreateClient().PostAsJsonAsync(
            "/api/person-conditions",
            new CreatePersonConditionRequest(person, condition),
            Json);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PersonConditionResponse>(Json))!.Id;
    }

    private async Task DeleteCase(int personConditionId)
    {
        if (personConditionId == 0)
        {
            return;
        }

        await using var context = server.NewContext();
        await context.PersonConditions
            .Where(report => report.Id == personConditionId)
            .ExecuteDeleteAsync();
    }

    private async Task<int> PersonId(string named)
    {
        using var response = await api.CreateClient().GetAsync("/api/persons?pageSize=200");
        response.EnsureSuccessStatusCode();

        var page = (await response.Content
            .ReadFromJsonAsync<PagedResult<PersonResponse>>(Json))!;

        return page.Items
            .Single(person => $"{person.LastName}, {person.FirstName}" == named)
            .Id;
    }

    private async Task<int> ConditionId(string named)
    {
        using var response = await api.CreateClient().GetAsync("/api/conditions?pageSize=200");
        response.EnsureSuccessStatusCode();

        var page = (await response.Content
            .ReadFromJsonAsync<PagedResult<ConditionResponse>>(Json))!;

        return page.Items.Single(condition => condition.Name == named).Id;
    }
}
