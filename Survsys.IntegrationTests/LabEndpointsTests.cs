using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The laboratory result round-trip, against the real database: a specimen
/// with a test and a result, recorded against the seeded report and read back
/// whole through <c>GET /api/labs/{id}</c>, carrying the values
/// <c>../nedss/docs/db-snapshots/session-011/02-after-lab-save.txt</c> dumps.
/// </summary>
/// <remarks>
/// The report is <c>Fixtures/person-conditions.sql</c>'s, found by the record
/// number <see cref="SqlServerFixture.SeededReportRecordNumber"/>. The report
/// morbidity-report tests in this collection clear the <c>person_conditions</c>
/// table, so this test re-seeds the report if it is gone before it runs, and
/// clears every specimen it records afterwards — a specimen left linked would
/// refuse those tests' delete, since a report with specimens against it is not
/// deletable. The suite does not skip when SQL Server is absent;
/// <see cref="SqlServerFixture"/> fails it loudly instead.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class LabEndpointsTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    /// <summary>
    /// The whole of the round-trip: a specimen carrying the session-011 lab
    /// values, one test and one result posted to the report, a 201 whose
    /// <c>Location</c> reads the specimen back field for field, the specimen's
    /// person taken from the report, and the join that links the specimen to
    /// the report.
    /// </summary>
    [Fact]
    public async Task ASpecimenRecordedAgainstAReportReadsBackWhole()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        try
        {
            var request = new CreateLabRequest(
                LabFacilityId: 11,
                ProviderId: 13,
                DiagnosticFacilityId: 17,
                ReportingFacilityId: 18,
                SpecimenSourceId: 12,
                CollectionDate: new DateOnly(2026, 3, 8),
                AccessionNo: "LAB-2026-011",
                SpecimenSentToState: null,
                Tests:
                [
                    new CreateLabTestRequest(
                        TestTypeId: 3,
                        TestStatusId: 1,
                        LoincCode: "5195-3",
                        ReferenceRange: "0-40",
                        LabTestDate: new DateOnly(2026, 3, 9),
                        Results:
                        [
                            new CreateLabTestResultRequest(
                                OrganismId: 5,
                                ResultValue: "Positive",
                                TestResult: "Positive/Reactive",
                                Comment: "from session-011",
                                Units: "titer",
                                PrimaryLabResult: true),
                        ]),
                ]);

            using var created = await client.PostAsJsonAsync(
                $"/api/person-conditions/{reportId}/labs", request, Json);

            Assert.Equal(HttpStatusCode.Created, created.StatusCode);

            var detail = (await created.Content.ReadFromJsonAsync<LabDetailResponse>(Json))!;
            var labId = detail.Specimen.Id;

            Assert.Equal($"/api/labs/{labId}", created.Headers.Location?.ToString());

            using var read = await client.GetAsync($"/api/labs/{labId}");
            read.EnsureSuccessStatusCode();

            var round = (await read.Content.ReadFromJsonAsync<LabDetailResponse>(Json))!;

            // The created specimen and the read one are the same specimen.
            Assert.Equal(detail.Specimen, round.Specimen);

            // The specimen carries the session-011 values, and its person is
            // the report's rather than any the request could have named.
            Assert.True(round.Specimen.Id > 0);
            Assert.Equal(await PersonOfReport(reportId), round.Specimen.PersonId);
            Assert.Equal(reportId, round.Specimen.PersonConditionId);
            Assert.Equal(11, round.Specimen.LabFacilityId);
            Assert.Equal(13, round.Specimen.ProviderId);
            Assert.Equal(17, round.Specimen.DiagnosticFacilityId);
            Assert.Equal(18, round.Specimen.ReportingFacilityId);
            Assert.Equal(12, round.Specimen.SpecimenSourceId);
            Assert.Equal(new DateOnly(2026, 3, 8), round.Specimen.CollectionDate);
            Assert.Equal("LAB-2026-011", round.Specimen.AccessionNo);
            Assert.Null(round.Specimen.SpecimenSentToState);

            // Its one test, with its one result.
            var test = Assert.Single(round.Tests);
            Assert.Equal(labId, test.LabId);
            Assert.Equal(3, test.TestTypeId);
            Assert.Equal(1, test.TestStatusId);
            Assert.Equal("5195-3", test.LoincCode);
            Assert.Equal("0-40", test.ReferenceRange);
            Assert.Equal(new DateOnly(2026, 3, 9), test.LabTestDate);

            var result = Assert.Single(test.Results);
            Assert.Equal(test.Id, result.LabTestId);
            Assert.Equal(5, result.OrganismId);
            Assert.Equal("Positive", result.ResultValue);
            Assert.Equal("Positive/Reactive", result.TestResult);
            Assert.Equal("from session-011", result.Comment);
            Assert.Equal("titer", result.Units);
            Assert.True(result.PrimaryLabResult);

            // No susceptibility was recorded, and none comes back.
            Assert.Empty(round.ResistTests);

            // The join links the specimen to the report it was recorded against.
            await using var context = server.NewContext();
            Assert.True(await context.PersonConditionLabs.AnyAsync(
                link => link.PersonConditionId == reportId && link.LabId == labId));
        }
        finally
        {
            await ClearSpecimensOf(reportId);
        }
    }

    /// <summary>
    /// An antimicrobial susceptibility row records against a specimen and reads
    /// back whole, hung off the specimen as the legacy table hangs it off the
    /// lab rather than off a test.
    /// </summary>
    [Fact]
    public async Task AnAntimicrobialSusceptibilityRecordedReadsBackWhole()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        try
        {
            var detail = await Record(client, reportId, new CreateLabRequest(
                AccessionNo: "LAB-2026-011",
                ResistTests:
                [
                    new CreateLabResistTestRequest(
                        OrganismId: 5,
                        ResistTestAgentId: 7,
                        ResistTestResultId: 2,
                        TestDate: new DateOnly(2026, 3, 10)),
                ]));

            using var read = await client.GetAsync($"/api/labs/{detail.Specimen.Id}");
            read.EnsureSuccessStatusCode();

            var round = (await read.Content.ReadFromJsonAsync<LabDetailResponse>(Json))!;

            Assert.Empty(round.Tests);

            var resist = Assert.Single(round.ResistTests);
            Assert.Equal(detail.Specimen.Id, resist.LabId);
            Assert.Equal(5, resist.OrganismId);
            Assert.Equal(7, resist.ResistTestAgentId);
            Assert.Equal(2, resist.ResistTestResultId);
            Assert.Equal(new DateOnly(2026, 3, 10), resist.TestDate);
        }
        finally
        {
            await ClearSpecimensOf(reportId);
        }
    }

    /// <summary>
    /// A report's specimens come back paged, ordered by id, and with the total
    /// across every page — three specimens read two at a time.
    /// </summary>
    [Fact]
    public async Task AReportsSpecimensComeBackPagedAndOrdered()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        try
        {
            var ids = new List<int>();

            for (var accession = 1; accession <= 3; accession++)
            {
                ids.Add((await Record(client, reportId,
                    new CreateLabRequest(AccessionNo: $"LAB-2026-{accession:D3}"))).Specimen.Id);
            }

            var first = await PageOfSpecimens(client, reportId, page: 1, pageSize: 2);
            var second = await PageOfSpecimens(client, reportId, page: 2, pageSize: 2);

            Assert.Equal(3, first.TotalCount);
            Assert.Equal(3, second.TotalCount);
            Assert.Equal(2, first.Items.Count);
            Assert.Single(second.Items);

            // Ordered by id across the pages, which is the total order the
            // endpoint promises: the three come back in the order they were
            // recorded, split at the page boundary and not repeated.
            Assert.Equal(
                ids.OrderBy(id => id),
                first.Items.Concat(second.Items).Select(specimen => specimen.Id));
        }
        finally
        {
            await ClearSpecimensOf(reportId);
        }
    }

    /// <summary>
    /// A list asked for against a report nobody has is a Problem Details, the
    /// same refusal recording a specimen against it gives — not an empty page
    /// that would read as a report with no specimens.
    /// </summary>
    [Fact]
    public async Task AListForAReportNobodyHasIsAProblemDetails()
    {
        using var response = await api.CreateClient()
            .GetAsync("/api/person-conditions/987654/labs");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = problem.RootElement;

        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Contains("personConditionId 987654 is not a case morbidity report on file",
            root.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("correlationId").GetString()));
    }

    /// <summary>
    /// Records a specimen that is expected to succeed, and answers with it.
    /// </summary>
    private async Task<LabDetailResponse> Record(
        HttpClient client, int reportId, CreateLabRequest request)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/person-conditions/{reportId}/labs", request, Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<LabDetailResponse>(Json))!;
    }

    /// <summary>One page of a report's specimens, read over the wire.</summary>
    private async Task<PagedResult<LabResponse>> PageOfSpecimens(
        HttpClient client, int reportId, int page, int pageSize)
    {
        using var response = await client.GetAsync(
            $"/api/person-conditions/{reportId}/labs?page={page}&pageSize={pageSize}");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PagedResult<LabResponse>>(Json))!;
    }

    /// <summary>
    /// The seeded report's id, re-seeding the report if a report test in this
    /// collection has cleared it since the fixture was built.
    /// </summary>
    private async Task<int> EnsureSeededReport()
    {
        await using var context = server.NewContext();

        var report = await context.PersonConditions.SingleOrDefaultAsync(
            report => report.RecordNumber == SqlServerFixture.SeededReportRecordNumber);

        if (report is not null)
        {
            return report.Id;
        }

        var personId = await context.Persons
            .Where(person => person.LastName == "Rodriguez" && person.FirstName == "Maria")
            .Select(person => person.Id)
            .SingleAsync();

        var conditionId = await context.Conditions
            .Where(condition => condition.Name == "Malaria")
            .Select(condition => condition.Id)
            .SingleAsync();

        report = new PersonCondition
        {
            PersonId = personId,
            ConditionId = conditionId,
            RecordNumber = SqlServerFixture.SeededReportRecordNumber,
            FirstReportedPhDate = new DateOnly(2026, 3, 12),
            EventOnsetDate = new DateOnly(2026, 3, 8),
            CreatedAt = DateTimeOffset.Now,
        };

        context.PersonConditions.Add(report);
        await context.SaveChangesAsync();

        return report.Id;
    }

    /// <summary>The person a report is about, read straight from the report.</summary>
    private async Task<int> PersonOfReport(int reportId)
    {
        await using var context = server.NewContext();

        return await context.PersonConditions
            .Where(report => report.Id == reportId)
            .Select(report => report.PersonId)
            .SingleAsync();
    }

    /// <summary>
    /// Takes every specimen this test recorded against the report out of the
    /// database — the join first, then the specimens, which cascade to their
    /// tests, results and susceptibility — and leaves the report, so that a
    /// report test's clear is not refused by a specimen still linked to it.
    /// </summary>
    private async Task ClearSpecimensOf(int reportId)
    {
        await using var context = server.NewContext();

        var labIds = await context.PersonConditionLabs
            .Where(link => link.PersonConditionId == reportId)
            .Select(link => link.LabId)
            .ToListAsync();

        await context.PersonConditionLabs
            .Where(link => link.PersonConditionId == reportId)
            .ExecuteDeleteAsync();

        if (labIds.Count > 0)
        {
            await context.Labs.Where(lab => labIds.Contains(lab.Id)).ExecuteDeleteAsync();
        }
    }
}
