using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// <c>GET /api/cases</c> against a real SQL Server: what each criterion
/// narrows the case morbidity reports to, how the pages partition them, and
/// what a row carries.
/// </summary>
/// <remarks>
/// Every test here asserts on cases this class creates itself, for three
/// people whose surnames — Quillfeather and Quillon — no fixture and no other
/// test uses, so that the rows the shared database accumulates from the rest
/// of the collection never change an answer. The cases are opened through the
/// API, moved and classified through the workflow and status routes, and
/// given their onset dates and MMWR weeks through the context, because no
/// route sets the event onset yet: the clinical route writes the disease
/// onset, a different column, and recalculating the event's week from a
/// changed onset is session 60's. Each test builds the four cases afresh and
/// takes them away again, with their people, so the person list the rest of
/// the collection counts is as it was.
///
/// A filter that could also match rows this class did not create — a
/// condition, an agency, a workflow state, a status, an onset range — is
/// asserted through <see cref="Mine"/>, the rows whose patient is one of the
/// three, in the order the page returned them; a filter that can match only
/// these rows is asserted on the whole page. The seeded Malaria report is
/// re-seeded if a report test has cleared it, as the lab and investigation
/// tests do, so the empty search has it to find.
///
/// The four cases, oldest record number first:
/// <list type="bullet">
/// <item>Quillfeather, Amara — Malaria, the first agency, accepted, confirmed, onset 2026-02-10 (MMWR week 7).</item>
/// <item>Quillfeather, Amos — Malaria, the second agency, assigned to LHD, unclassified, onset 2026-02-20.</item>
/// <item>Quillon, Bea — the other condition, the first agency, under investigation, probable, onset 2026-03-05.</item>
/// <item>Quillfeather, Amara — the other condition, no agency, assigned to LHD, unclassified, onset 2026-03-15.</item>
/// </list>
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class CaseSearchEndpointsTests(SqlServerFixture server) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    private HttpClient client = null!;

    private int amara;
    private int amos;
    private int bea;

    private int malaria;
    private int otherCondition;
    private string otherConditionName = "";

    private AgencyResponse firstAgency = null!;
    private AgencyResponse secondAgency = null!;

    private PersonConditionResponse amaraMalaria = null!;
    private PersonConditionResponse amosMalaria = null!;
    private PersonConditionResponse beaOther = null!;
    private PersonConditionResponse amaraOther = null!;

    public async Task InitializeAsync()
    {
        client = api.CreateClient();

        await EnsureSeededReport();

        amara = await CreatePerson("Quillfeather", "Amara");
        amos = await CreatePerson("Quillfeather", "Amos");
        bea = await CreatePerson("Quillon", "Bea");

        var conditions = await Page<ConditionResponse>("/api/conditions?pageSize=200");
        malaria = conditions.Items.Single(condition => condition.Name == "Malaria").Id;
        var other = conditions.Items
            .Where(condition => condition.Active && condition.Name != "Malaria")
            .OrderBy(condition => condition.Name, StringComparer.Ordinal)
            .First();
        otherCondition = other.Id;
        otherConditionName = other.Name;

        var agencies = await Page<AgencyResponse>("/api/agencies");
        var byId = agencies.Items.OrderBy(agency => agency.Id).ToList();
        firstAgency = byId[0];
        secondAgency = byId[1];

        amaraMalaria = await OpenCase(amara, malaria, firstAgency.Id);
        amosMalaria = await OpenCase(amos, malaria, secondAgency.Id);
        beaOther = await OpenCase(bea, otherCondition, firstAgency.Id);
        amaraOther = await OpenCase(amara, otherCondition, agencyId: null);

        await Move(amaraMalaria.Id, WorkflowRules.AcceptedCode);
        await Classify(amaraMalaria.Id, CaseStatusRules.ConfirmedCode);

        await Move(beaOther.Id, WorkflowRules.AcceptedCode);
        await Move(beaOther.Id, WorkflowRules.UnderInvestigationCode);
        await Classify(beaOther.Id, CaseStatusRules.ProbableCode);

        await SetOnset(amaraMalaria.Id, new DateOnly(2026, 2, 10), week: 7, year: 2026);
        await SetOnset(amosMalaria.Id, new DateOnly(2026, 2, 20), week: 8, year: 2026);
        await SetOnset(beaOther.Id, new DateOnly(2026, 3, 5), week: 10, year: 2026);
        await SetOnset(amaraOther.Id, new DateOnly(2026, 3, 15), week: 11, year: 2026);
    }

    public async Task DisposeAsync()
    {
        await using (var context = server.NewContext())
        {
            int[] cases = [amaraMalaria?.Id ?? 0, amosMalaria?.Id ?? 0, beaOther?.Id ?? 0, amaraOther?.Id ?? 0];
            int[] people = [amara, amos, bea];

            await context.PersonConditions
                .Where(report => cases.Contains(report.Id))
                .ExecuteDeleteAsync();
            await context.Persons
                .Where(person => people.Contains(person.Id))
                .ExecuteDeleteAsync();
        }

        client.Dispose();
        api.Dispose();
    }

    [Fact]
    public async Task TheEmptySearchCarriesEveryCaseNewestRecordNumberFirstWithTheSeededReportAmongThem()
    {
        var page = await Cases("?pageSize=200");

        Assert.Equal(
            [amaraOther.Id, beaOther.Id, amosMalaria.Id, amaraMalaria.Id],
            Mine(page));
        Assert.Equal(1, page.Page);
        Assert.Equal(200, page.PageSize);
        Assert.True(page.TotalCount >= 5, $"{page.TotalCount} rows, where the four opened here and the seeded report make five.");

        var seeded = Assert.Single(page.Items,
            row => row.RecordNumber == SqlServerFixture.SeededReportRecordNumber);
        Assert.Equal(SqlServerFixture.SeededReportPerson, seeded.PatientName);
        Assert.Equal(SqlServerFixture.SeededReportCondition, seeded.ConditionName);
        Assert.Null(seeded.AgencyId);
        Assert.Null(seeded.AgencyName);
        Assert.Equal(WorkflowRules.InitialCode, seeded.WorkflowState);

        // The four opened here took record numbers after the seeded one's,
        // so newest first puts every one of them before it.
        var rows = page.Items.Select(row => row.Id).ToList();
        Assert.True(
            Mine(page).All(id => rows.IndexOf(id) < rows.IndexOf(seeded.Id)),
            "A case opened in this test came after the seeded report, which is older.");
    }

    /// <summary>
    /// The row carries what the Events list shows per case: the patient by
    /// name, the condition and agency by name, the state, the status, the
    /// MMWR week and the onset, with the identifiers to open each.
    /// </summary>
    [Fact]
    public async Task ARowCarriesThePatientDiseaseAgencyStateStatusWeekAndOnset()
    {
        var page = await Cases($"?personId={amara}");

        var row = Assert.Single(page.Items, row => row.Id == amaraMalaria.Id);
        Assert.Equal(
            new CaseSearchItemResponse(
                amaraMalaria.Id,
                amaraMalaria.RecordNumber,
                amara,
                "Quillfeather, Amara",
                malaria,
                "Malaria",
                firstAgency.Id,
                firstAgency.Name,
                WorkflowRules.AcceptedCode,
                CaseStatusRules.ConfirmedCode,
                7,
                2026,
                new DateOnly(2026, 2, 10)),
            row);

        var unassigned = Assert.Single(page.Items, row => row.Id == amaraOther.Id);
        Assert.Equal(otherConditionName, unassigned.ConditionName);
        Assert.Null(unassigned.AgencyId);
        Assert.Null(unassigned.AgencyName);
        Assert.Equal(WorkflowRules.InitialCode, unassigned.WorkflowState);
        Assert.Null(unassigned.CaseStatus);
    }

    [Fact]
    public async Task ARecordNumberPrefixNarrowsToTheCasesThatBeginWithIt()
    {
        var whole = await Cases($"?recordNumber={beaOther.RecordNumber}");
        var year = await Cases($"?recordNumber={beaOther.RecordNumber[..4]}&pageSize=200");

        Assert.Equal([beaOther.Id], whole.Items.Select(row => row.Id));
        Assert.Equal(
            [amaraOther.Id, beaOther.Id, amosMalaria.Id, amaraMalaria.Id],
            Mine(year));
    }

    /// <summary>
    /// A prefix of the surname: "Quillf" finds the two Quillfeathers' three
    /// cases and not Quillon's, and nobody else's, since no other test names a
    /// person so.
    /// </summary>
    [Fact]
    public async Task ALastNamePrefixNarrowsToThosePatientsCases()
    {
        var page = await Cases("?lastName=Quillf");

        Assert.Equal(
            [amaraOther.Id, amosMalaria.Id, amaraMalaria.Id],
            page.Items.Select(row => row.Id));
    }

    [Fact]
    public async Task AFirstNamePrefixNarrowsToThosePatientsCases()
    {
        var page = await Cases("?firstName=Amo&pageSize=200");

        Assert.Equal([amosMalaria.Id], Mine(page));
    }

    [Fact]
    public async Task AConditionNarrowsToTheCasesOnIt()
    {
        var page = await Cases($"?conditionId={otherCondition}&pageSize=200");

        Assert.Equal([amaraOther.Id, beaOther.Id], Mine(page));
        Assert.All(page.Items, row => Assert.Equal(otherCondition, row.ConditionId));
    }

    [Fact]
    public async Task AnAgencyNarrowsToTheCasesItInvestigates()
    {
        var page = await Cases($"?agencyId={firstAgency.Id}&pageSize=200");

        Assert.Equal([beaOther.Id, amaraMalaria.Id], Mine(page));
        Assert.All(page.Items, row => Assert.Equal(firstAgency.Id, row.AgencyId));
    }

    [Fact]
    public async Task APersonNarrowsToTheirOwnCases()
    {
        var page = await Cases($"?personId={amara}");

        Assert.Equal([amaraOther.Id, amaraMalaria.Id], page.Items.Select(row => row.Id));
        Assert.Equal(2, page.TotalCount);
    }

    /// <summary>
    /// Each of the three states a case here sits in finds the cases at it,
    /// the initial one included: a case opened and never moved is stored at
    /// the initial code, and one whose code was never stored at all reads as
    /// it too.
    /// </summary>
    [Fact]
    public async Task AWorkflowStateNarrowsToTheCasesAtIt()
    {
        var accepted = await Cases($"?workflowState={WorkflowRules.AcceptedCode}&pageSize=200");
        var investigating = await Cases($"?workflowState={WorkflowRules.UnderInvestigationCode}&pageSize=200");
        var assigned = await Cases($"?workflowState={WorkflowRules.InitialCode}&pageSize=200");

        Assert.Equal([amaraMalaria.Id], Mine(accepted));
        Assert.Equal([beaOther.Id], Mine(investigating));
        Assert.Equal([amaraOther.Id, amosMalaria.Id], Mine(assigned));
        Assert.All(assigned.Items, row => Assert.Equal(WorkflowRules.InitialCode, row.WorkflowState));
    }

    [Fact]
    public async Task ACaseStatusNarrowsToTheCasesClassifiedSo()
    {
        var confirmed = await Cases($"?caseStatus={CaseStatusRules.ConfirmedCode}&pageSize=200");
        var probable = await Cases($"?caseStatus={CaseStatusRules.ProbableCode}&pageSize=200");

        Assert.Equal([amaraMalaria.Id], Mine(confirmed));
        Assert.Equal([beaOther.Id], Mine(probable));
    }

    /// <summary>
    /// Either end alone, and both together: the range is inclusive at both
    /// ends, which the last case proves with a <c>from</c> on one case's onset
    /// and a <c>to</c> on another's.
    /// </summary>
    [Fact]
    public async Task AnOnsetRangeNarrowsToTheCasesWithinItInclusive()
    {
        var from = await Cases("?onsetFrom=2026-03-01&pageSize=200");
        var to = await Cases("?onsetTo=2026-02-28&pageSize=200");
        var both = await Cases("?onsetFrom=2026-02-20&onsetTo=2026-03-05&pageSize=200");

        Assert.Equal([amaraOther.Id, beaOther.Id], Mine(from));
        Assert.Equal([amosMalaria.Id, amaraMalaria.Id], Mine(to));
        Assert.Equal([beaOther.Id, amosMalaria.Id], Mine(both));
    }

    /// <summary>
    /// Criteria together are their intersection: the same surname and
    /// condition narrowed by one agency find one case, by the other agency
    /// find the other, and by a condition neither has find none.
    /// </summary>
    [Fact]
    public async Task TheCriteriaCombinedNarrowToTheirIntersection()
    {
        var first = await Cases(
            $"?lastName=Quillfeather&conditionId={malaria}&agencyId={firstAgency.Id}"
            + $"&workflowState={WorkflowRules.AcceptedCode}&caseStatus={CaseStatusRules.ConfirmedCode}"
            + "&onsetFrom=2026-02-01&onsetTo=2026-02-28");
        var second = await Cases($"?lastName=Quillfeather&conditionId={malaria}&agencyId={secondAgency.Id}");
        var none = await Cases($"?lastName=Quillfeather&conditionId={otherCondition}&agencyId={firstAgency.Id}");

        Assert.Equal([amaraMalaria.Id], first.Items.Select(row => row.Id));
        Assert.Equal([amosMalaria.Id], second.Items.Select(row => row.Id));
        Assert.Empty(none.Items);
        Assert.Equal(0, none.TotalCount);
    }

    /// <summary>
    /// A blank text criterion is one nobody gave, so a form submitted with its
    /// boxes empty searches as the empty search does.
    /// </summary>
    [Fact]
    public async Task ABlankCriterionFiltersNothing()
    {
        var page = await Cases("?recordNumber=&lastName=%20&firstName=&workflowState=&caseStatus=&pageSize=200");

        Assert.Equal(
            [amaraOther.Id, beaOther.Id, amosMalaria.Id, amaraMalaria.Id],
            Mine(page));
    }

    [Fact]
    public async Task ThePagesPartitionTheResultInOrderWithTheTotalOnEveryPage()
    {
        var first = await Cases($"?personId={amara}&page=1&pageSize=1");
        var second = await Cases($"?personId={amara}&page=2&pageSize=1");
        var third = await Cases($"?personId={amara}&page=3&pageSize=1");

        Assert.Equal([amaraOther.Id], first.Items.Select(row => row.Id));
        Assert.Equal([amaraMalaria.Id], second.Items.Select(row => row.Id));
        Assert.Empty(third.Items);

        foreach (var (page, number) in new[] { (first, 1), (second, 2), (third, 3) })
        {
            Assert.Equal(number, page.Page);
            Assert.Equal(1, page.PageSize);
            Assert.Equal(2, page.TotalCount);
        }
    }

    [Theory]
    [InlineData("?page=0", "page must be 1 or greater")]
    [InlineData("?pageSize=0", "pageSize must be 1 or greater")]
    [InlineData("?pageSize=201", "pageSize must be 200 or fewer")]
    public async Task APageOutsideItsBoundsIsAProblemDetails(string query, string expected)
    {
        using var response = await client.GetAsync("/api/cases" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = problem.RootElement;

        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Contains(expected, root.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("correlationId").GetString()));
    }

    /// <summary>
    /// The ids of the rows on a page whose patient is one of this class's
    /// three, in the order the page returned them.
    /// </summary>
    private List<int> Mine(PagedResult<CaseSearchItemResponse> page)
    {
        int[] people = [amara, amos, bea];

        return page.Items
            .Where(row => people.Contains(row.PersonId))
            .Select(row => row.Id)
            .ToList();
    }

    private Task<PagedResult<CaseSearchItemResponse>> Cases(string query) =>
        Page<CaseSearchItemResponse>("/api/cases" + query);

    private async Task<PagedResult<T>> Page<T>(string url)
    {
        using var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PagedResult<T>>(Json))!;
    }

    private async Task<int> CreatePerson(string lastName, string firstName)
    {
        using var created = await client.PostAsJsonAsync(
            "/api/persons", new CreatePersonRequest(lastName, firstName), Json);
        created.EnsureSuccessStatusCode();
        return (await created.Content.ReadFromJsonAsync<PersonResponse>(Json))!.Id;
    }

    private async Task<PersonConditionResponse> OpenCase(int personId, int conditionId, int? agencyId)
    {
        using var created = await client.PostAsJsonAsync(
            "/api/person-conditions",
            new CreatePersonConditionRequest(personId, conditionId, agencyId, new DateOnly(2026, 3, 12)),
            Json);
        created.EnsureSuccessStatusCode();
        return (await created.Content.ReadFromJsonAsync<PersonConditionResponse>(Json))!;
    }

    /// <summary>
    /// Moves a report one step forward to a state, by the transition that
    /// reaches it from the state before it: the search filters by state, and a
    /// move is asked for by transition.
    /// </summary>
    private async Task Move(int id, string workflowState)
    {
        var transition = workflowState switch
        {
            WorkflowRules.AcceptedCode => WorkflowRules.AcceptCode,
            WorkflowRules.UnderInvestigationCode => WorkflowRules.InvestigateCode,
            WorkflowRules.ClosedCode => WorkflowRules.CloseCode,
            _ => throw new ArgumentOutOfRangeException(nameof(workflowState), workflowState, "is not a state a forward step reaches."),
        };

        using var moved = await client.PutAsJsonAsync(
            $"/api/person-conditions/{id}/workflow", new WorkflowTransitionRequest(transition), Json);
        moved.EnsureSuccessStatusCode();
    }

    private async Task Classify(int id, string caseStatus)
    {
        using var classified = await client.PutAsJsonAsync(
            $"/api/person-conditions/{id}/status", new UpdateCaseStatusRequest(caseStatus), Json);
        classified.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// The event onset and its MMWR week, written through the context: no
    /// route changes the event onset yet, and the week is set beside it so
    /// that the row's week is one this test chose rather than the week the
    /// case happened to be opened in.
    /// </summary>
    private async Task SetOnset(int id, DateOnly onset, int week, int year)
    {
        await using var context = server.NewContext();

        await context.PersonConditions
            .Where(report => report.Id == id)
            .ExecuteUpdateAsync(set => set
                .SetProperty(report => report.EventOnsetDate, onset)
                .SetProperty(report => report.MmwrWeek, week)
                .SetProperty(report => report.MmwrYear, year));
    }

    /// <summary>
    /// The seeded report's id, re-seeding the report if a report test in this
    /// collection has cleared it since the fixture was built — the same
    /// re-seed <see cref="LabEndpointsTests"/> and
    /// <see cref="InvestigationAdministrativeEndpointsTests"/> make.
    /// </summary>
    private async Task EnsureSeededReport()
    {
        await using var context = server.NewContext();

        if (await context.PersonConditions.AnyAsync(
            report => report.RecordNumber == SqlServerFixture.SeededReportRecordNumber))
        {
            return;
        }

        var personId = await context.Persons
            .Where(person => person.LastName == "Rodriguez" && person.FirstName == "Maria")
            .Select(person => person.Id)
            .SingleAsync();

        var conditionId = await context.Conditions
            .Where(condition => condition.Name == SqlServerFixture.SeededReportCondition)
            .Select(condition => condition.Id)
            .SingleAsync();

        context.PersonConditions.Add(new PersonCondition
        {
            PersonId = personId,
            ConditionId = conditionId,
            RecordNumber = SqlServerFixture.SeededReportRecordNumber,
            FirstReportedPhDate = new DateOnly(2026, 3, 12),
            EventOnsetDate = new DateOnly(2026, 3, 8),
            CreatedAt = DateTimeOffset.Now,
        });

        await context.SaveChangesAsync();
    }
}
