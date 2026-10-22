using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The CSV import every reference table shares, against the real database and
/// driven through a code list: a preview reports the rows it would add, change
/// and refuse and writes none of them; the confirmed import writes them and
/// keeps a row the file leaves out; and a list's export imported back changes
/// nothing.
/// </summary>
/// <remarks>
/// Every code a test adds starts with <see cref="Marker"/>, and those codes are
/// deleted before and after each test; the seeded codes are only read.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class ReferenceImportTests(SqlServerFixture server) : IDisposable
{
    private const string Marker = "ITEST-IMP-";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task APreviewWritesNothingAndTheConfirmedImportWritesWhatItReported()
    {
        await Clear();
        var client = api.CreateClient();

        try
        {
            await Add(client, new SaveCodeRequest($"{Marker}A", "Alpha"));
            await Add(client, new SaveCodeRequest($"{Marker}B", "Beta"));
            await Add(client, new SaveCodeRequest($"{Marker}KEPT", "Left out of the file"));

            // Line 2 matches its code, line 3 changes one, line 4 is new, and
            // lines 5 and 6 cannot be saved.
            var csv = "code,description,sort_order,active\r\n"
                + $"{Marker}A,Alpha,0,yes\r\n"
                + $"{Marker}B,Beta renamed,0,yes\r\n"
                + $"{Marker}C,Gamma,,\r\n"
                + ",No code,,\r\n"
                + $"{Marker}D,Delta,first,\r\n";

            var preview = await Import(client, CodeLists.Race, csv, confirm: false);

            Assert.False(preview.Applied);
            Assert.Equal([new ReferenceImportRow(4, $"{Marker}C")], preview.Added);
            Assert.Equal([new ReferenceImportRow(3, $"{Marker}B")], preview.Changed);
            Assert.Equal([5, 6], preview.Refused.Select(problem => problem.Line));
            Assert.Contains("code is required", preview.Refused[0].Reason);
            Assert.Contains("sort_order", preview.Refused[1].Reason);
            Assert.Equal(
                [($"{Marker}A", "Alpha"), ($"{Marker}B", "Beta"), ($"{Marker}KEPT", "Left out of the file")],
                await Ours(client));

            var confirmed = await Import(client, CodeLists.Race, csv, confirm: true);

            Assert.True(confirmed.Applied);
            Assert.Equal(preview.Added, confirmed.Added);
            Assert.Equal(preview.Changed, confirmed.Changed);
            Assert.Equal(
                [
                    ($"{Marker}A", "Alpha"),
                    ($"{Marker}B", "Beta renamed"),
                    ($"{Marker}C", "Gamma"),
                    ($"{Marker}KEPT", "Left out of the file"),
                ],
                await Ours(client));
        }
        finally
        {
            await Clear();
        }
    }

    [Theory]
    [InlineData(CodeLists.County)]
    [InlineData(CodeLists.TestType)]
    [InlineData(CodeLists.CaseStatus)]
    public async Task AListsExportImportedBackChangesNothing(string list)
    {
        var client = api.CreateClient();

        var exported = await client.GetStringAsync($"/api/codes/{list}/csv");
        var preview = await Import(client, list, exported, confirm: false);

        Assert.Empty(preview.Added);
        Assert.Empty(preview.Changed);
        Assert.Empty(preview.Refused);
    }

    private static async Task Add(HttpClient client, SaveCodeRequest request)
    {
        using var response = await client.PostAsJsonAsync($"/api/codes/{CodeLists.Race}", request, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<ReferenceImportResponse> Import(HttpClient client, string list, string csv, bool confirm)
    {
        using var posted = await client.PostAsync(
            $"/api/codes/{list}/csv{(confirm ? "?confirm=true" : "")}", new StringContent(csv, Encoding.UTF8, "text/csv"));
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        return (await posted.Content.ReadFromJsonAsync<ReferenceImportResponse>(Json))!;
    }

    /// <summary>This test's race codes, each with its description, in code order.</summary>
    private static async Task<List<(string Code, string? Description)>> Ours(HttpClient client) =>
        (await client.GetFromJsonAsync<List<CodeResponse>>($"/api/codes/{CodeLists.Race}?inactive=true", Json))!
            .Where(code => code.Code.StartsWith(Marker, StringComparison.Ordinal))
            .Select(code => (code.Code, code.Description))
            .OrderBy(code => code.Code, StringComparer.Ordinal)
            .ToList();

    private async Task Clear()
    {
        await using var context = server.NewContext();

        await context.CodeValues.Where(code => code.Code.StartsWith(Marker)).ExecuteDeleteAsync();
    }
}
