using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// <c>GET /api/conditions</c> over the real database: the whole vertical
/// slice, from the migration through the query and the mapping to the JSON a
/// screen will read.
/// </summary>
/// <remarks>
/// The host starts through <see cref="SurvsysApi"/>, over
/// <see cref="WebApplicationFactory{TEntryPoint}"/>, which runs
/// <c>Program.cs</c> as written — the startup migration gate included. It
/// passing here is itself an assertion: the fixture migrated the database
/// before the host read its pending-migration list.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class ConditionEndpointsTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task TheFirstPageCarriesTheSeededConditionsInNameOrder()
    {
        var page = await Conditions("/api/conditions");

        Assert.Equal(
            SqlServerFixture.ConditionsByName,
            page.Items.Select(condition => condition.Name));
        Assert.Equal(SqlServerFixture.SeededConditions, page.TotalCount);
        Assert.Equal(1, page.Page);
        Assert.Equal(50, page.PageSize);
    }

    /// <summary>
    /// The pages partition the collection: every row once, in order, and
    /// nothing on two pages. This is what the ordering is for — an unordered
    /// query can return one row twice and another not at all, and still look
    /// right read one page at a time.
    /// </summary>
    [Fact]
    public async Task ThePagesPartitionTheCollectionInOrder()
    {
        var first = await Conditions("/api/conditions?page=1&pageSize=3");
        var second = await Conditions("/api/conditions?page=2&pageSize=3");
        var third = await Conditions("/api/conditions?page=3&pageSize=3");

        Assert.Equal(3, first.Items.Count);
        Assert.Equal(3, second.Items.Count);
        Assert.Single(third.Items);

        var paged = first.Items
            .Concat(second.Items)
            .Concat(third.Items)
            .Select(condition => condition.Name);

        Assert.Equal(SqlServerFixture.ConditionsByName, paged);

        foreach (var page in new[] { first, second, third })
        {
            Assert.Equal(SqlServerFixture.SeededConditions, page.TotalCount);
            Assert.Equal(3, page.PageSize);
        }
    }

    [Fact]
    public async Task APagePastTheLastRowIsEmptyRatherThanAnError()
    {
        var page = await Conditions("/api/conditions?page=99&pageSize=3");

        Assert.Empty(page.Items);
        Assert.Equal(SqlServerFixture.SeededConditions, page.TotalCount);
    }

    /// <summary>
    /// Four of the eight stored columns reach the wire, and a condition with
    /// no CDC code carries a null rather than the empty string the legacy
    /// table used.
    /// </summary>
    [Fact]
    public async Task AConditionCarriesTheFourWireFieldsAndNothingElse()
    {
        using var response = await api.CreateClient().GetAsync("/api/conditions?pageSize=200");
        var body = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();

        var conditions = JsonSerializer.Deserialize<PagedResult<ConditionResponse>>(body, Json)!.Items;

        var bartonellosis = conditions.Single(condition => condition.Name == "Bartonellosis");
        Assert.Null(bartonellosis.CdcCode);
        Assert.True(bartonellosis.Active);

        var influenza = conditions.Single(
            condition => condition.Name == "Influenza, animal isolates");
        Assert.Equal("11070", influenza.CdcCode);
        Assert.False(influenza.Active);

        // The three lead-in columns are not on the wire at all, because the
        // projection EF Core translates never selects them. These are the two
        // seeded rows whose lead-in prose is long enough to be unmistakable.
        Assert.DoesNotContain("rarely transmitted", body, StringComparison.Ordinal);
        Assert.DoesNotContain("two separate questions", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("?page=0", "page must be 1 or greater")]
    [InlineData("?page=-3", "page must be 1 or greater")]
    [InlineData("?pageSize=0", "pageSize must be 1 or greater")]
    [InlineData("?pageSize=201", "pageSize must be 200 or fewer")]
    public async Task APageOutsideItsBoundsIsAProblemDetails(string query, string expected)
    {
        using var response = await api.CreateClient().GetAsync("/api/conditions" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = problem.RootElement;

        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Contains(expected, root.GetProperty("detail").GetString(), StringComparison.Ordinal);

        // The correlation id the caller quotes back. Every Problem Details
        // body this tier returns carries one.
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("correlationId").GetString()));
    }

    /// <summary>
    /// A page size that is not a number at all is the framework's own refusal
    /// rather than the endpoint's, and it is Problem Details too — so a
    /// caller never has to read two error shapes from one endpoint.
    /// </summary>
    [Fact]
    public async Task APageSizeThatIsNotANumberIsAlsoAProblemDetails()
    {
        using var response = await api.CreateClient().GetAsync("/api/conditions?pageSize=many");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// A condition file previewed writes nothing; confirmed, it adds the row
    /// with a blank id, lead-in line breaks and all, saves over the row with an
    /// id, and refuses a nameless row and an id not on file; and the export
    /// imported back changes nothing. The conditions it adds are deleted after.
    /// </summary>
    [Fact]
    public async Task AConditionFileIsPreviewedThenImportedAndExported()
    {
        var client = api.CreateClient();
        await ClearImported();

        try
        {
            using var created = await client.PostAsJsonAsync(
                "/api/conditions", new SaveConditionRequest("Itest condition A", true), Json);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var a = (await created.Content.ReadFromJsonAsync<ConditionDetailResponse>(Json))!;

            var csv = "id,name,active,cdc_code,contact_lead_in,place_lead_in,treatment_lead_in\r\n"
                + $"{a.Id},Itest condition A,no,99999,,,\r\n"
                + ",Itest condition B,yes,,\"Name every contact.\r\nThen call each.\",,\r\n"
                + ",,yes,,,,\r\n"
                + "987654321,Itest condition C,yes,,,,\r\n";

            var preview = await Import(client, csv, confirm: false);

            Assert.Equal([new ReferenceImportRow(2, $"{a.Id}")], preview.Changed);
            Assert.Equal([new ReferenceImportRow(3, "Itest condition B")], preview.Added);
            Assert.Equal([5, 6], preview.Refused.Select(problem => problem.Line));
            Assert.Contains("name is required", preview.Refused[0].Reason);
            Assert.Contains("is not a condition on file", preview.Refused[1].Reason);
            Assert.True((await Detail(client, a.Id)).Active);

            await Import(client, csv, confirm: true);

            Assert.Equal((false, "99999"), ((await Detail(client, a.Id)).Active, (await Detail(client, a.Id)).CdcCode));

            var exported = await client.GetStringAsync("/api/conditions/csv");
            Assert.Contains("Itest condition B,yes,,\"Name every contact.\r\nThen call each.\",,\r\n", exported);

            var again = await Import(client, exported, confirm: false);
            Assert.Empty(again.Added);
            Assert.Empty(again.Changed);
            Assert.Empty(again.Refused);
        }
        finally
        {
            await ClearImported();
        }
    }

    /// <summary>
    /// A new condition has no print criteria; put, they read back in code order
    /// each once, and put again they replace what was there.
    /// </summary>
    [Fact]
    public async Task PrintCriteriaAreReplacedAndReadBack()
    {
        var client = api.CreateClient();
        await ClearImported();

        try
        {
            using var created = await client.PostAsJsonAsync(
                "/api/conditions", new SaveConditionRequest("Itest condition criteria", true), Json);
            var condition = (await created.Content.ReadFromJsonAsync<ConditionDetailResponse>(Json))!;
            var url = $"/api/conditions/{condition.Id}/print-criteria";

            Assert.Empty((await client.GetFromJsonAsync<ConditionPrintCriteriaResponse>(url, Json))!.CaseStatuses);

            using var first = await client.PutAsJsonAsync(url, new ConditionPrintCriteriaRequest(["P", "C", "P"]), Json);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(["C", "P"], (await first.Content.ReadFromJsonAsync<ConditionPrintCriteriaResponse>(Json))!.CaseStatuses);

            using var second = await client.PutAsJsonAsync(url, new ConditionPrintCriteriaRequest(["C", "S"]), Json);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            Assert.Equal(["C", "S"], (await client.GetFromJsonAsync<ConditionPrintCriteriaResponse>(url, Json))!.CaseStatuses);
        }
        finally
        {
            await ClearImported();
        }
    }

    /// <summary>
    /// A code that is not a case status, or no list at all, is refused, and an unknown condition is
    /// a 404 for both the read and the write.
    /// </summary>
    [Fact]
    public async Task PrintCriteriaRefuseAnUnknownStatusOrCondition()
    {
        var client = api.CreateClient();
        var someCondition = (await Conditions("/api/conditions?pageSize=1")).Items[0].Id;

        using var refused = await client.PutAsJsonAsync(
            $"/api/conditions/{someCondition}/print-criteria", new ConditionPrintCriteriaRequest(["X"]), Json);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        using var nullList = await client.PutAsync(
            $"/api/conditions/{someCondition}/print-criteria",
            new StringContent("""{ "caseStatuses": null }""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, nullList.StatusCode);

        using var missingRead = await client.GetAsync("/api/conditions/987654321/print-criteria");
        Assert.Equal(HttpStatusCode.NotFound, missingRead.StatusCode);

        using var missingWrite = await client.PutAsJsonAsync(
            "/api/conditions/987654321/print-criteria", new ConditionPrintCriteriaRequest(["C"]), Json);
        Assert.Equal(HttpStatusCode.NotFound, missingWrite.StatusCode);
    }

    private static async Task<ReferenceImportResponse> Import(HttpClient client, string csv, bool confirm)
    {
        using var posted = await client.PostAsync(
            $"/api/conditions/csv{(confirm ? "?confirm=true" : "")}", new StringContent(csv, Encoding.UTF8, "text/csv"));
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        return (await posted.Content.ReadFromJsonAsync<ReferenceImportResponse>(Json))!;
    }

    private static async Task<ConditionDetailResponse> Detail(HttpClient client, int id) =>
        (await client.GetFromJsonAsync<ConditionDetailResponse>($"/api/conditions/{id}", Json))!;

    private async Task ClearImported()
    {
        await using var context = server.NewContext();
        await context.Conditions.Where(condition => condition.Name.StartsWith("Itest condition")).ExecuteDeleteAsync();
    }

    private async Task<PagedResult<ConditionResponse>> Conditions(string url)
    {
        using var response = await api.CreateClient().GetAsync(url);

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PagedResult<ConditionResponse>>(Json))!;
    }
}
