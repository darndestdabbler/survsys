using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The outbreak endpoints, against the real database: that an outbreak is
/// created and read back with its condition and owning agency named, that a
/// save writes over it, that the list comes back newest first, that a bad
/// reference and an unknown id are refused, and that the multi-agency access
/// is replaced whole and read back by name.
/// </summary>
/// <remarks>
/// Driven through the endpoints. The outbreaks table is cleared before and
/// after each test, which cascades to the access rows, so each test starts and
/// ends with no outbreaks; the seeded agencies and conditions are left as they
/// are, since the suite shares the one database and nothing here changes them.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class OutbreakEndpointsTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task AnOutbreakIsCreatedAndReadBackWithItsReferencesNamed()
    {
        await Clear();

        try
        {
            var condition = await ConditionId("Malaria");
            var (agencyId, agencyName) = await AnAgency();

            var created = await Open(new SaveOutbreakRequest(
                "Spring picnic",
                "2026-001",
                new DateOnly(2026, 3, 1),
                "A gathering made people ill",
                ConditionId: condition,
                AgencyId: agencyId,
                Status: 0));

            Assert.Equal("Spring picnic", created.Name);
            Assert.Equal("2026-001", created.Number);
            Assert.Equal(condition, created.ConditionId);
            Assert.Equal("Malaria", created.ConditionName);
            Assert.Equal(agencyId, created.AgencyId);
            Assert.Equal(agencyName, created.AgencyName);
            Assert.Empty(created.Access);

            using var detail = await api.CreateClient().GetAsync($"/api/outbreaks/{created.Id}");
            detail.EnsureSuccessStatusCode();
            var read = (await detail.Content.ReadFromJsonAsync<OutbreakDetailResponse>(Json))!;
            Assert.Equal("Spring picnic", read.Name);
            Assert.Equal("Malaria", read.ConditionName);
            Assert.Equal(agencyName, read.AgencyName);
        }
        finally
        {
            await Clear();
        }
    }

    [Fact]
    public async Task AnOutbreakWithNoReferencesIsAllowed()
    {
        await Clear();

        try
        {
            var created = await Open(new SaveOutbreakRequest("Unknown source"));

            Assert.Equal("Unknown source", created.Name);
            Assert.Null(created.ConditionId);
            Assert.Null(created.ConditionName);
            Assert.Null(created.AgencyId);
            Assert.Null(created.AgencyName);
            Assert.Equal(0, created.Status);
        }
        finally
        {
            await Clear();
        }
    }

    [Fact]
    public async Task ASaveWritesOverTheOutbreak()
    {
        await Clear();

        try
        {
            var created = await Open(new SaveOutbreakRequest("Spring picnic"));

            using var response = await api.CreateClient().PutAsJsonAsync(
                $"/api/outbreaks/{created.Id}",
                new SaveOutbreakRequest("Summer fair", "2026-002", Status: 1),
                Json);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var saved = (await response.Content.ReadFromJsonAsync<OutbreakDetailResponse>(Json))!;
            Assert.Equal("Summer fair", saved.Name);
            Assert.Equal("2026-002", saved.Number);
            Assert.Equal(1, saved.Status);
        }
        finally
        {
            await Clear();
        }
    }

    [Fact]
    public async Task TheListComesBackNewestFirst()
    {
        await Clear();

        try
        {
            var first = await Open(new SaveOutbreakRequest("First"));
            var second = await Open(new SaveOutbreakRequest("Second"));

            using var response = await api.CreateClient().GetAsync("/api/outbreaks?pageSize=200");
            response.EnsureSuccessStatusCode();
            var page = (await response.Content
                .ReadFromJsonAsync<PagedResult<OutbreakResponse>>(Json))!;

            var ours = page.Items.Where(outbreak => outbreak.Id == first.Id || outbreak.Id == second.Id)
                .ToList();
            Assert.Equal(2, ours.Count);
            Assert.Equal(second.Id, ours[0].Id);
            Assert.Equal(first.Id, ours[1].Id);
        }
        finally
        {
            await Clear();
        }
    }

    [Fact]
    public async Task AnUnknownOutbreakIsAFourOhFour()
    {
        using var get = await api.CreateClient().GetAsync("/api/outbreaks/987654321");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

        using var put = await api.CreateClient().PutAsJsonAsync(
            "/api/outbreaks/987654321",
            new SaveOutbreakRequest("Nowhere"),
            Json);
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
    }

    [Fact]
    public async Task AnOutbreakWithNoNameIsRefused()
    {
        using var response = await api.CreateClient().PostAsJsonAsync(
            "/api/outbreaks",
            new SaveOutbreakRequest(""),
            Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnUnknownConditionOrAgencyIsRefused()
    {
        using var badCondition = await api.CreateClient().PostAsJsonAsync(
            "/api/outbreaks",
            new SaveOutbreakRequest("Bad condition", ConditionId: 987654321),
            Json);
        Assert.Equal(HttpStatusCode.BadRequest, badCondition.StatusCode);

        using var badAgency = await api.CreateClient().PostAsJsonAsync(
            "/api/outbreaks",
            new SaveOutbreakRequest("Bad agency", AgencyId: 987654321),
            Json);
        Assert.Equal(HttpStatusCode.BadRequest, badAgency.StatusCode);
    }

    [Fact]
    public async Task AnOutbreakIsDeletedWithItsAccessAndUnlinksItsCases()
    {
        await Clear();
        int reportId = 0;

        try
        {
            var outbreak = await Open(new SaveOutbreakRequest("Spring picnic"));
            var agencies = await TwoAgencies();

            // Give it access, a linked case and an activity entry, so the
            // delete has cascades and a set-null to exercise.
            using var access = await api.CreateClient().PutAsJsonAsync(
                $"/api/outbreaks/{outbreak.Id}/access",
                new SetOutbreakAccessRequest([agencies[0].Id]),
                Json);
            access.EnsureSuccessStatusCode();

            var person = await PersonId("Rodriguez, Maria");
            var condition = await ConditionId("Malaria");
            using var create = await api.CreateClient().PostAsJsonAsync(
                "/api/person-conditions",
                new CreatePersonConditionRequest(person, condition),
                Json);
            var report = (await create.Content.ReadFromJsonAsync<PersonConditionResponse>(Json))!;
            reportId = report.Id;

            using var link = await api.CreateClient().PutAsJsonAsync(
                $"/api/person-conditions/{reportId}/administrative",
                new UpdateAdministrativeRequest(
                    null, null, null, null, null, null, null, null, null, outbreak.Id),
                Json);
            link.EnsureSuccessStatusCode();

            using var deleted = await api.CreateClient().DeleteAsync($"/api/outbreaks/{outbreak.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

            using var gone = await api.CreateClient().GetAsync($"/api/outbreaks/{outbreak.Id}");
            Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);

            // The case survives the outbreak, with its link nulled.
            await using var context = server.NewContext();
            var survivor = await context.PersonConditions
                .SingleAsync(saved => saved.Id == reportId);
            Assert.Null(survivor.OutbreakId);
        }
        finally
        {
            if (reportId != 0)
            {
                await using var context = server.NewContext();
                await context.PersonConditions
                    .Where(saved => saved.Id == reportId)
                    .ExecuteDeleteAsync();
            }

            await Clear();
        }
    }

    [Fact]
    public async Task DeletingAnUnknownOutbreakIsAFourOhFour()
    {
        using var response = await api.CreateClient().DeleteAsync("/api/outbreaks/987654321");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TheAccessIsReplacedWholeAndReadBackByName()
    {
        await Clear();

        try
        {
            var created = await Open(new SaveOutbreakRequest("Spring picnic"));
            var agencies = await TwoAgencies();

            using var put = await api.CreateClient().PutAsJsonAsync(
                $"/api/outbreaks/{created.Id}/access",
                new SetOutbreakAccessRequest([agencies[0].Id, agencies[1].Id, agencies[0].Id]),
                Json);
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);

            var access = (await put.Content
                .ReadFromJsonAsync<IReadOnlyList<OutbreakAccessResponse>>(Json))!;
            Assert.Equal(2, access.Count);
            Assert.Equal(
                agencies.Select(agency => agency.Name).OrderBy(name => name, StringComparer.Ordinal),
                access.Select(row => row.AgencyName));

            using var narrow = await api.CreateClient().PutAsJsonAsync(
                $"/api/outbreaks/{created.Id}/access",
                new SetOutbreakAccessRequest([agencies[0].Id]),
                Json);
            Assert.Equal(HttpStatusCode.OK, narrow.StatusCode);

            using var get = await api.CreateClient().GetAsync($"/api/outbreaks/{created.Id}/access");
            get.EnsureSuccessStatusCode();
            var narrowed = (await get.Content
                .ReadFromJsonAsync<IReadOnlyList<OutbreakAccessResponse>>(Json))!;
            Assert.Equal(agencies[0].Id, Assert.Single(narrowed).AgencyId);
        }
        finally
        {
            await Clear();
        }
    }

    [Fact]
    public async Task AccessRefusesAnUnknownAgencyAndAnUnknownOutbreak()
    {
        await Clear();

        try
        {
            var created = await Open(new SaveOutbreakRequest("Spring picnic"));

            using var badAgency = await api.CreateClient().PutAsJsonAsync(
                $"/api/outbreaks/{created.Id}/access",
                new SetOutbreakAccessRequest([987654321]),
                Json);
            Assert.Equal(HttpStatusCode.BadRequest, badAgency.StatusCode);

            using var badOutbreak = await api.CreateClient().PutAsJsonAsync(
                "/api/outbreaks/987654321/access",
                new SetOutbreakAccessRequest([]),
                Json);
            Assert.Equal(HttpStatusCode.NotFound, badOutbreak.StatusCode);
        }
        finally
        {
            await Clear();
        }
    }

    [Fact]
    public async Task TheReportIsWrittenSectionBySectionAndReadBackWhole()
    {
        await Clear();

        try
        {
            var created = await Open(new SaveOutbreakRequest("Spring picnic"));
            var author = await MyUserId();

            var sections = await ReportSections();
            Assert.Equal(7, sections.Count);

            // Every section comes back before anything is written, each with no text.
            using var empty = await api.CreateClient().GetAsync($"/api/outbreaks/{created.Id}/report");
            empty.EnsureSuccessStatusCode();
            var blank = (await empty.Content
                .ReadFromJsonAsync<IReadOnlyList<OutbreakReportSectionResponse>>(Json))!;
            Assert.Equal(7, blank.Count);
            Assert.All(blank, section => Assert.Null(section.ReportText));

            var first = sections[0].Id;
            using var write = await api.CreateClient().PutAsJsonAsync(
                $"/api/outbreaks/{created.Id}/report/{first}",
                new SaveOutbreakReportSectionRequest(author, "The picnic was on the first."),
                Json);
            Assert.Equal(HttpStatusCode.OK, write.StatusCode);

            var report = (await write.Content
                .ReadFromJsonAsync<IReadOnlyList<OutbreakReportSectionResponse>>(Json))!;
            Assert.Equal(7, report.Count);
            Assert.Equal(
                "The picnic was on the first.",
                report.Single(section => section.ReportSectionId == first).ReportText);

            // Writing the same section again saves over the one row.
            using var rewrite = await api.CreateClient().PutAsJsonAsync(
                $"/api/outbreaks/{created.Id}/report/{first}",
                new SaveOutbreakReportSectionRequest(author, "Corrected: the second."),
                Json);
            Assert.Equal(HttpStatusCode.OK, rewrite.StatusCode);
            var corrected = (await rewrite.Content
                .ReadFromJsonAsync<IReadOnlyList<OutbreakReportSectionResponse>>(Json))!;
            Assert.Equal(
                "Corrected: the second.",
                corrected.Single(section => section.ReportSectionId == first).ReportText);
            Assert.Equal(7, corrected.Count);
        }
        finally
        {
            await Clear();
        }
    }

    [Fact]
    public async Task WritingAReportRefusesAnUnknownSectionAndAnUnknownOutbreak()
    {
        await Clear();

        try
        {
            var created = await Open(new SaveOutbreakRequest("Spring picnic"));
            var author = await MyUserId();

            using var badSection = await api.CreateClient().PutAsJsonAsync(
                $"/api/outbreaks/{created.Id}/report/987654321",
                new SaveOutbreakReportSectionRequest(author, "Nowhere"),
                Json);
            Assert.Equal(HttpStatusCode.NotFound, badSection.StatusCode);

            var sections = await ReportSections();
            using var badOutbreak = await api.CreateClient().PutAsJsonAsync(
                $"/api/outbreaks/987654321/report/{sections[0].Id}",
                new SaveOutbreakReportSectionRequest(author, "Nowhere"),
                Json);
            Assert.Equal(HttpStatusCode.NotFound, badOutbreak.StatusCode);
        }
        finally
        {
            await Clear();
        }
    }

    [Fact]
    public async Task TheActivityLogAppendsAndListsNewestFirst()
    {
        await Clear();

        try
        {
            var created = await Open(new SaveOutbreakRequest("Spring picnic"));
            var author = await MyUserId();

            using var first = await api.CreateClient().PostAsJsonAsync(
                $"/api/outbreaks/{created.Id}/activity",
                new AddOutbreakUpdateRequest(author, "Opened the outbreak.", 0),
                Json);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            var firstEntry = (await first.Content.ReadFromJsonAsync<OutbreakUpdateResponse>(Json))!;

            using var second = await api.CreateClient().PostAsJsonAsync(
                $"/api/outbreaks/{created.Id}/activity",
                new AddOutbreakUpdateRequest(author, "Linked the first cases.", 0),
                Json);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            var secondEntry = (await second.Content.ReadFromJsonAsync<OutbreakUpdateResponse>(Json))!;

            using var response = await api.CreateClient().GetAsync($"/api/outbreaks/{created.Id}/activity");
            response.EnsureSuccessStatusCode();
            var log = (await response.Content
                .ReadFromJsonAsync<IReadOnlyList<OutbreakUpdateResponse>>(Json))!;

            Assert.Equal(2, log.Count);
            Assert.Equal(secondEntry.Id, log[0].Id);
            Assert.Equal(firstEntry.Id, log[1].Id);
            Assert.Equal("Linked the first cases.", log[0].Description);
        }
        finally
        {
            await Clear();
        }
    }

    [Fact]
    public async Task TheLinkedCasesListNamesEachCasesPersonAndCondition()
    {
        await Clear();

        var outbreak = await Open(new SaveOutbreakRequest("Spring picnic"));
        int reportId = 0;

        try
        {
            var person = await PersonId("Rodriguez, Maria");
            var condition = await ConditionId("Malaria");

            using var create = await api.CreateClient().PostAsJsonAsync(
                "/api/person-conditions",
                new CreatePersonConditionRequest(person, condition),
                Json);
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            var report = (await create.Content.ReadFromJsonAsync<PersonConditionResponse>(Json))!;
            reportId = report.Id;

            using var link = await api.CreateClient().PutAsJsonAsync(
                $"/api/person-conditions/{reportId}/administrative",
                new UpdateAdministrativeRequest(
                    null, null, null, null, null, null, null, null, null, outbreak.Id),
                Json);
            link.EnsureSuccessStatusCode();

            using var response = await api.CreateClient().GetAsync($"/api/outbreaks/{outbreak.Id}/cases");
            response.EnsureSuccessStatusCode();
            var cases = (await response.Content
                .ReadFromJsonAsync<IReadOnlyList<OutbreakCaseResponse>>(Json))!;

            var row = Assert.Single(cases);
            Assert.Equal(reportId, row.ReportId);
            Assert.Equal(report.RecordNumber, row.RecordNumber);
            Assert.Equal("Rodriguez, Maria", row.PersonName);
            Assert.Equal("Malaria", row.Condition);

            using var missing = await api.CreateClient().GetAsync("/api/outbreaks/987654321/cases");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }
        finally
        {
            if (reportId != 0)
            {
                await using var context = server.NewContext();
                await context.PersonConditions
                    .Where(report => report.Id == reportId)
                    .ExecuteDeleteAsync();
            }

            await Clear();
        }
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

    private async Task<IReadOnlyList<ReportSectionResponse>> ReportSections()
    {
        using var response = await api.CreateClient().GetAsync("/api/report-sections");
        response.EnsureSuccessStatusCode();

        return (await response.Content
            .ReadFromJsonAsync<IReadOnlyList<ReportSectionResponse>>(Json))!;
    }

    private async Task<int> MyUserId()
    {
        using var response = await api.CreateClient().GetAsync("/api/me");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CurrentUserResponse>(Json))!.Id;
    }

    /// <summary>Creates an outbreak and answers its detail.</summary>
    private async Task<OutbreakDetailResponse> Open(SaveOutbreakRequest request)
    {
        using var response = await api.CreateClient().PostAsJsonAsync("/api/outbreaks", request, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<OutbreakDetailResponse>(Json))!;
    }

    private async Task<int> ConditionId(string named)
    {
        using var response = await api.CreateClient().GetAsync("/api/conditions?pageSize=200");
        response.EnsureSuccessStatusCode();

        var page = (await response.Content
            .ReadFromJsonAsync<PagedResult<ConditionResponse>>(Json))!;

        return page.Items.Single(condition => condition.Name == named).Id;
    }

    private async Task<(int Id, string Name)> AnAgency()
    {
        var agencies = await Agencies();
        var agency = agencies[0];
        return (agency.Id, agency.Name);
    }

    private async Task<IReadOnlyList<AgencyResponse>> TwoAgencies()
    {
        var agencies = await Agencies();
        return [agencies[0], agencies[1]];
    }

    private async Task<IReadOnlyList<AgencyResponse>> Agencies()
    {
        using var response = await api.CreateClient().GetAsync("/api/agencies");
        response.EnsureSuccessStatusCode();

        var page = (await response.Content
            .ReadFromJsonAsync<PagedResult<AgencyResponse>>(Json))!;

        return page.Items;
    }

    /// <summary>
    /// Takes every outbreak out of the table, which cascades to the access
    /// rows, so each test starts and ends with no outbreaks.
    /// </summary>
    private async Task Clear()
    {
        await using var context = server.NewContext();

        await context.Outbreaks.ExecuteDeleteAsync();
    }
}
