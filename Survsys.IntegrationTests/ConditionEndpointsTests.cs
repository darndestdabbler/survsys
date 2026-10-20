using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
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

    private async Task<PagedResult<ConditionResponse>> Conditions(string url)
    {
        using var response = await api.CreateClient().GetAsync(url);

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PagedResult<ConditionResponse>>(Json))!;
    }
}
