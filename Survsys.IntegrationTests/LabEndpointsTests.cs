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

    /// <summary>The last name of the providers this class records, and clears.</summary>
    private const string ClinicianLastName = "LabEndpointsTests-Clinician";

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
        var providerId = await RecordProvider();
        var client = api.CreateClient();

        try
        {
            var request = new CreateLabRequest(
                LabFacilityId: 11,
                ProviderId: providerId,
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
            Assert.Equal(providerId, round.Specimen.ProviderId);
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
    /// A specimen's own columns are corrected through <c>PUT /api/labs/{id}</c>:
    /// the response and a fresh read both carry the new values, and the save is
    /// audited as an update of the specimen's row.
    /// </summary>
    [Fact]
    public async Task ASpecimenEditChangesItsColumnsAndIsAudited()
    {
        var reportId = await EnsureSeededReport();
        var providerId = await RecordProvider();
        var client = api.CreateClient();

        try
        {
            var labId = (await Record(client, reportId,
                new CreateLabRequest(LabFacilityId: 11, AccessionNo: "LAB-2026-011"))).Specimen.Id;

            using var put = await client.PutAsJsonAsync($"/api/labs/{labId}",
                new UpdateLabRequest(
                    LabFacilityId: 99,
                    ProviderId: providerId,
                    AccessionNo: "LAB-2026-011-CORRECTED",
                    SpecimenSentToState: true),
                Json);

            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            var updated = (await put.Content.ReadFromJsonAsync<LabResponse>(Json))!;
            Assert.Equal(99, updated.LabFacilityId);
            Assert.Equal(providerId, updated.ProviderId);
            Assert.Equal("LAB-2026-011-CORRECTED", updated.AccessionNo);
            Assert.True(updated.SpecimenSentToState);

            // The person and the report the specimen belongs to are untouched.
            Assert.Equal(reportId, updated.PersonConditionId);

            using var read = await client.GetAsync($"/api/labs/{labId}");
            read.EnsureSuccessStatusCode();
            var round = (await read.Content.ReadFromJsonAsync<LabDetailResponse>(Json))!;
            Assert.Equal(99, round.Specimen.LabFacilityId);
            Assert.Equal("LAB-2026-011-CORRECTED", round.Specimen.AccessionNo);

            Assert.Contains("Update", await AuditOperations("labs", labId));
        }
        finally
        {
            await ClearSpecimensOf(reportId);
        }
    }

    /// <summary>
    /// A test's own columns are corrected through <c>PUT /api/lab-tests/{id}</c>,
    /// leaving its results in place, and the save is audited.
    /// </summary>
    [Fact]
    public async Task ATestEditChangesItsColumnsAndKeepsItsResultsAndIsAudited()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        try
        {
            var detail = await Record(client, reportId, new CreateLabRequest(
                AccessionNo: "LAB-2026-011",
                Tests:
                [
                    new CreateLabTestRequest(
                        TestTypeId: 3,
                        LoincCode: "5195-3",
                        Results: [new CreateLabTestResultRequest(TestResult: "Positive/Reactive", PrimaryLabResult: true)]),
                ]));

            var testId = detail.Tests.Single().Id;

            using var put = await client.PutAsJsonAsync($"/api/lab-tests/{testId}",
                new UpdateLabTestRequest(TestTypeId: 4, TestStatusId: 2, LoincCode: "1988-5"),
                Json);

            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            var updated = (await put.Content.ReadFromJsonAsync<LabTestResponse>(Json))!;
            Assert.Equal(4, updated.TestTypeId);
            Assert.Equal(2, updated.TestStatusId);
            Assert.Equal("1988-5", updated.LoincCode);

            // The result the test had is still hung off it after the edit.
            var keptResult = Assert.Single(updated.Results);
            Assert.Equal("Positive/Reactive", keptResult.TestResult);

            Assert.Contains("Update", await AuditOperations("lab_tests", testId));
        }
        finally
        {
            await ClearSpecimensOf(reportId);
        }
    }

    /// <summary>
    /// A result is corrected through <c>PUT /api/lab-test-results/{id}</c> and
    /// the save is audited.
    /// </summary>
    [Fact]
    public async Task AResultEditChangesItAndIsAudited()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        try
        {
            var detail = await Record(client, reportId, new CreateLabRequest(
                AccessionNo: "LAB-2026-011",
                Tests:
                [
                    new CreateLabTestRequest(
                        Results: [new CreateLabTestResultRequest(TestResult: "Pending", PrimaryLabResult: true)]),
                ]));

            var resultId = detail.Tests.Single().Results.Single().Id;

            using var put = await client.PutAsJsonAsync($"/api/lab-test-results/{resultId}",
                new CreateLabTestResultRequest(
                    OrganismId: 5, ResultValue: "1200", TestResult: "Positive/Reactive",
                    Units: "U/L", PrimaryLabResult: true),
                Json);

            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            var updated = (await put.Content.ReadFromJsonAsync<LabTestResultResponse>(Json))!;
            Assert.Equal("Positive/Reactive", updated.TestResult);
            Assert.Equal("1200", updated.ResultValue);
            Assert.Equal(5, updated.OrganismId);

            Assert.Contains("Update", await AuditOperations("lab_test_results", resultId));
        }
        finally
        {
            await ClearSpecimensOf(reportId);
        }
    }

    /// <summary>
    /// Marking a result the primary one is refused, as a 400 Problem Details,
    /// where another result on the same test already is: a test shows one
    /// primary result and no more.
    /// </summary>
    [Fact]
    public async Task AResultEditRefusedWhenAnotherResultIsAlreadyPrimary()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        try
        {
            var detail = await Record(client, reportId, new CreateLabRequest(
                AccessionNo: "LAB-2026-011",
                Tests:
                [
                    new CreateLabTestRequest(
                        Results:
                        [
                            new CreateLabTestResultRequest(TestResult: "Positive/Reactive", PrimaryLabResult: true),
                            new CreateLabTestResultRequest(TestResult: "Negative/Non-reactive"),
                        ]),
                ]));

            var secondResultId = detail.Tests.Single().Results[1].Id;

            using var put = await client.PutAsJsonAsync($"/api/lab-test-results/{secondResultId}",
                new CreateLabTestResultRequest(TestResult: "Negative/Non-reactive", PrimaryLabResult: true),
                Json);

            Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
            Assert.Equal("application/problem+json", put.Content.Headers.ContentType?.MediaType);

            using var problem = JsonDocument.Parse(await put.Content.ReadAsStringAsync());
            Assert.Contains("already the primary one",
                problem.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
        }
        finally
        {
            await ClearSpecimensOf(reportId);
        }
    }

    /// <summary>
    /// A susceptibility row is corrected through
    /// <c>PUT /api/lab-resist-tests/{id}</c> and the save is audited.
    /// </summary>
    [Fact]
    public async Task ASusceptibilityEditChangesItAndIsAudited()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        try
        {
            var detail = await Record(client, reportId, new CreateLabRequest(
                AccessionNo: "LAB-2026-011",
                ResistTests: [new CreateLabResistTestRequest(OrganismId: 5, ResistTestAgentId: 7)]));

            var resistId = detail.ResistTests.Single().Id;

            using var put = await client.PutAsJsonAsync($"/api/lab-resist-tests/{resistId}",
                new CreateLabResistTestRequest(
                    OrganismId: 5, ResistTestAgentId: 8, ResistTestResultId: 2,
                    TestDate: new DateOnly(2026, 3, 11)),
                Json);

            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            var updated = (await put.Content.ReadFromJsonAsync<LabResistTestResponse>(Json))!;
            Assert.Equal(8, updated.ResistTestAgentId);
            Assert.Equal(2, updated.ResistTestResultId);
            Assert.Equal(new DateOnly(2026, 3, 11), updated.TestDate);

            Assert.Contains("Update", await AuditOperations("lab_resist_tests", resistId));
        }
        finally
        {
            await ClearSpecimensOf(reportId);
        }
    }

    /// <summary>
    /// The leaves delete freely and each delete is audited: a result and a
    /// susceptibility row are removed with a 204, gone on the next read, and
    /// recorded as a delete of their row.
    /// </summary>
    [Fact]
    public async Task ALeafResultAndSusceptibilityDeleteAndAreAudited()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        try
        {
            var detail = await Record(client, reportId, new CreateLabRequest(
                AccessionNo: "LAB-2026-011",
                Tests:
                [
                    new CreateLabTestRequest(
                        Results: [new CreateLabTestResultRequest(TestResult: "Positive/Reactive")]),
                ],
                ResistTests: [new CreateLabResistTestRequest(OrganismId: 5)]));

            var resultId = detail.Tests.Single().Results.Single().Id;
            var resistId = detail.ResistTests.Single().Id;

            using var deleteResult = await client.DeleteAsync($"/api/lab-test-results/{resultId}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResult.StatusCode);

            using var deleteResist = await client.DeleteAsync($"/api/lab-resist-tests/{resistId}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResist.StatusCode);

            using var read = await client.GetAsync($"/api/labs/{detail.Specimen.Id}");
            read.EnsureSuccessStatusCode();
            var round = (await read.Content.ReadFromJsonAsync<LabDetailResponse>(Json))!;
            Assert.Empty(round.Tests.Single().Results);
            Assert.Empty(round.ResistTests);

            Assert.Contains("Delete", await AuditOperations("lab_test_results", resultId));
            Assert.Contains("Delete", await AuditOperations("lab_resist_tests", resistId));
        }
        finally
        {
            await ClearSpecimensOf(reportId);
        }
    }

    /// <summary>
    /// A test with its results already removed deletes with a 204, and a
    /// specimen with nothing left against it deletes with a 204, its join to
    /// the report going with it and the delete audited.
    /// </summary>
    [Fact]
    public async Task ATestAndThenItsSpecimenDeleteWhenNothingDependsOnThem()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        var labId = (await Record(client, reportId, new CreateLabRequest(
            AccessionNo: "LAB-2026-011",
            Tests: [new CreateLabTestRequest(TestTypeId: 3)]))).Specimen.Id;

        int testId;

        using (var readTests = await client.GetAsync($"/api/labs/{labId}"))
        {
            readTests.EnsureSuccessStatusCode();
            var detail = (await readTests.Content.ReadFromJsonAsync<LabDetailResponse>(Json))!;
            testId = detail.Tests.Single().Id;

            using var deleteTest = await client.DeleteAsync($"/api/lab-tests/{testId}");
            Assert.Equal(HttpStatusCode.NoContent, deleteTest.StatusCode);
        }

        // The test delete is a delete route like the others, and it is audited.
        Assert.Contains("Delete", await AuditOperations("lab_tests", testId));

        using var deleteLab = await client.DeleteAsync($"/api/labs/{labId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteLab.StatusCode);

        // The specimen is gone, and so is the join that linked it to the report.
        using var readGone = await client.GetAsync($"/api/labs/{labId}");
        Assert.Equal(HttpStatusCode.NotFound, readGone.StatusCode);

        await using var context = server.NewContext();
        Assert.False(await context.PersonConditionLabs.AnyAsync(
            link => link.PersonConditionId == reportId && link.LabId == labId));

        Assert.Contains("Delete", await AuditOperations("labs", labId));
    }

    /// <summary>
    /// A specimen that still has a test, and a test that still has a result,
    /// are each refused with a 409 Problem Details naming what depends on them
    /// rather than left to a foreign key to cascade.
    /// </summary>
    [Fact]
    public async Task ADeleteIsRefusedWhileRowsDependOnIt()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        try
        {
            var detail = await Record(client, reportId, new CreateLabRequest(
                AccessionNo: "LAB-2026-011",
                Tests:
                [
                    new CreateLabTestRequest(
                        Results: [new CreateLabTestResultRequest(TestResult: "Positive/Reactive")]),
                ]));

            var labId = detail.Specimen.Id;
            var testId = detail.Tests.Single().Id;

            using var deleteLab = await client.DeleteAsync($"/api/labs/{labId}");
            Assert.Equal(HttpStatusCode.Conflict, deleteLab.StatusCode);
            Assert.Equal("application/problem+json", deleteLab.Content.Headers.ContentType?.MediaType);
            using (var problem = JsonDocument.Parse(await deleteLab.Content.ReadAsStringAsync()))
            {
                var detailText = problem.RootElement.GetProperty("detail").GetString();
                Assert.Contains("still has 1 test", detailText, StringComparison.Ordinal);
            }

            using var deleteTest = await client.DeleteAsync($"/api/lab-tests/{testId}");
            Assert.Equal(HttpStatusCode.Conflict, deleteTest.StatusCode);
            using (var problem = JsonDocument.Parse(await deleteTest.Content.ReadAsStringAsync()))
            {
                Assert.Contains("still has 1 result",
                    problem.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
            }

            // Refused, so everything is still there to be read.
            using var read = await client.GetAsync($"/api/labs/{labId}");
            read.EnsureSuccessStatusCode();
        }
        finally
        {
            await ClearSpecimensOf(reportId);
        }
    }

    /// <summary>
    /// A specimen naming an ordering clinician who is not a provider on file is
    /// refused with a 400 Problem Details, as an unknown person on a contact
    /// is, and nothing is recorded against the report.
    /// </summary>
    [Fact]
    public async Task ASpecimenNamingNoProviderOnFileIsRefused()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        try
        {
            using var response = await client.PostAsJsonAsync(
                $"/api/person-conditions/{reportId}/labs",
                new CreateLabRequest(ProviderId: 987654, AccessionNo: "LAB-2026-011"),
                Json);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            using (var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
            {
                Assert.Contains("providerId 987654 is not a provider on file",
                    problem.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
            }

            Assert.Equal(0, (await PageOfSpecimens(client, reportId, page: 1, pageSize: 10)).TotalCount);
        }
        finally
        {
            await ClearSpecimensOf(reportId);
        }
    }

    /// <summary>
    /// An edit naming an ordering clinician who is not a provider on file is
    /// refused with a 400 Problem Details, and the specimen is left as it was.
    /// </summary>
    [Fact]
    public async Task ASpecimenEditNamingNoProviderOnFileIsRefused()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        try
        {
            var labId = (await Record(client, reportId,
                new CreateLabRequest(AccessionNo: "LAB-2026-011"))).Specimen.Id;

            using var put = await client.PutAsJsonAsync($"/api/labs/{labId}",
                new UpdateLabRequest(ProviderId: 987654, AccessionNo: "LAB-2026-011-CORRECTED"),
                Json);

            Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);

            using (var problem = JsonDocument.Parse(await put.Content.ReadAsStringAsync()))
            {
                Assert.Contains("providerId 987654 is not a provider on file",
                    problem.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
            }

            using var read = await client.GetAsync($"/api/labs/{labId}");
            read.EnsureSuccessStatusCode();
            var round = (await read.Content.ReadFromJsonAsync<LabDetailResponse>(Json))!;
            Assert.Null(round.Specimen.ProviderId);
            Assert.Equal("LAB-2026-011", round.Specimen.AccessionNo);
        }
        finally
        {
            await ClearSpecimensOf(reportId);
        }
    }

    /// <summary>
    /// An edit or a delete of a row nobody has is a 404 Problem Details, the
    /// resource named in the route not being there to change.
    /// </summary>
    [Fact]
    public async Task AnEditOrDeleteOfARowNobodyHasIsA404()
    {
        var client = api.CreateClient();

        using var put = await client.PutAsJsonAsync("/api/labs/987654",
            new UpdateLabRequest(LabFacilityId: 1), Json);
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);

        using var delete = await client.DeleteAsync("/api/lab-tests/987654");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
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

    /// <summary>
    /// Records a provider for a specimen to name as its ordering clinician, and
    /// answers with their id; <see cref="ClearSpecimensOf"/> takes them out again.
    /// </summary>
    private async Task<int> RecordProvider()
    {
        await using var context = server.NewContext();

        var provider = new Provider { FirstName = "Ada", LastName = ClinicianLastName };
        context.Providers.Add(provider);
        await context.SaveChangesAsync();

        return provider.Id;
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

    /// <summary>
    /// The operations audited against a table for one single-column key, in the
    /// order they were written — the AuditInterceptor's rows read back through
    /// a fresh context, so an update or a delete can be shown to have been
    /// recorded as well as to have happened.
    /// </summary>
    private async Task<List<string>> AuditOperations(string table, int key)
    {
        await using var context = server.NewContext();

        return await context.Set<AuditEntry>()
            .Where(entry => entry.TableName == table && entry.KeyValues == key.ToString())
            .OrderBy(entry => entry.Id)
            .Select(entry => entry.Operation)
            .ToListAsync();
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
    /// The providers <see cref="RecordProvider"/> recorded go after the
    /// specimens that named them.
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

        await context.Providers
            .Where(provider => provider.LastName == ClinicianLastName)
            .ExecuteDeleteAsync();
    }

    /// <summary>
    /// Recording a specimen on a case its print criteria notify queues the
    /// case, and so does deleting it again.
    /// </summary>
    [Fact]
    public async Task ALabWriteQueuesANotifiableCase()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();
        await MakeNotifiable(reportId);
        int? labId = null;

        try
        {
            labId = (await Record(client, reportId, new CreateLabRequest(AccessionNo: "LAB-QUEUE-1"))).Specimen.Id;
            Assert.True(await IsQueued(reportId));

            await MakeNotifiable(reportId);
            using var deleted = await client.DeleteAsync($"/api/labs/{labId}");
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            labId = null;
            Assert.True(await IsQueued(reportId));
        }
        finally
        {
            if (labId is { } id)
            {
                await client.DeleteAsync($"/api/labs/{id}");
            }

            await MakeUnclassified(reportId);
        }
    }

    /// <summary>
    /// Every lab edit and delete route — specimen, test, result and
    /// susceptibility — queues a case its print criteria notify, and the same
    /// writes on an unclassified case leave it off the queue.
    /// </summary>
    [Fact]
    public async Task EachLabEditAndDeleteQueuesOnlyANotifiableCase()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        try
        {
            foreach (var notifiable in new[] { true, false })
            {
                var detail = await Record(client, reportId, new CreateLabRequest(
                    AccessionNo: "LAB-QUEUE-2",
                    Tests:
                    [
                        new CreateLabTestRequest(
                            TestTypeId: 3,
                            Results: [new CreateLabTestResultRequest(TestResult: "Positive/Reactive")]),
                    ],
                    ResistTests: [new CreateLabResistTestRequest(OrganismId: 5)]));
                var labId = detail.Specimen.Id;
                var testId = detail.Tests.Single().Id;
                var resultId = detail.Tests.Single().Results.Single().Id;
                var resistId = detail.ResistTests.Single().Id;

                var writes = new (string Route, Func<Task<HttpResponseMessage>> Write)[]
                {
                    ("PUT lab", () => client.PutAsJsonAsync($"/api/labs/{labId}", new UpdateLabRequest(AccessionNo: "LAB-QUEUE-3"), Json)),
                    ("PUT test", () => client.PutAsJsonAsync($"/api/lab-tests/{testId}", new UpdateLabTestRequest(TestTypeId: 4), Json)),
                    ("PUT result", () => client.PutAsJsonAsync($"/api/lab-test-results/{resultId}", new CreateLabTestResultRequest(TestResult: "Negative"), Json)),
                    ("PUT resist", () => client.PutAsJsonAsync($"/api/lab-resist-tests/{resistId}", new CreateLabResistTestRequest(OrganismId: 6), Json)),
                    ("DELETE result", () => client.DeleteAsync($"/api/lab-test-results/{resultId}")),
                    ("DELETE resist", () => client.DeleteAsync($"/api/lab-resist-tests/{resistId}")),
                    ("DELETE test", () => client.DeleteAsync($"/api/lab-tests/{testId}")),
                    ("DELETE lab", () => client.DeleteAsync($"/api/labs/{labId}")),
                };

                foreach (var (route, write) in writes)
                {
                    if (notifiable)
                    {
                        await MakeNotifiable(reportId);
                    }
                    else
                    {
                        await MakeUnclassified(reportId);
                    }

                    using var response = await write();
                    Assert.True(response.IsSuccessStatusCode, $"{route} answered {(int)response.StatusCode}");
                    Assert.True(await IsQueued(reportId) == notifiable, $"{route} on a {(notifiable ? "notifiable" : "unclassified")} case");
                }
            }
        }
        finally
        {
            await ClearSpecimensOf(reportId);
            await MakeUnclassified(reportId);
        }
    }

    /// <summary>
    /// Gives the report a status its seeded print criteria notify and takes it
    /// off the queue, so a write that follows is what queues it.
    /// </summary>
    private async Task MakeNotifiable(int reportId)
    {
        await using var context = server.NewContext();
        await context.PersonConditions.Where(report => report.Id == reportId)
            .ExecuteUpdateAsync(set => set.SetProperty(report => report.CaseStatus, CaseStatusRules.ConfirmedCode));
        await context.NotificationQueue.Where(entry => entry.PersonConditionId == reportId).ExecuteDeleteAsync();
    }

    /// <summary>Puts the report back unclassified and off the queue.</summary>
    private async Task MakeUnclassified(int reportId)
    {
        await using var context = server.NewContext();
        await context.PersonConditions.Where(report => report.Id == reportId)
            .ExecuteUpdateAsync(set => set.SetProperty(report => report.CaseStatus, (string?)null));
        await context.NotificationQueue.Where(entry => entry.PersonConditionId == reportId).ExecuteDeleteAsync();
    }

    private async Task<bool> IsQueued(int reportId)
    {
        await using var context = server.NewContext();
        return await context.NotificationQueue.AnyAsync(entry => entry.PersonConditionId == reportId);
    }
}
