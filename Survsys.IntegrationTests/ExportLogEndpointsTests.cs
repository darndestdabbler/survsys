using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The export-log endpoints, against the real database: that the log reads
/// back newest first, that one export reads whole with the message generated
/// for it, and that an id no export has is a 404.
/// </summary>
/// <remarks>
/// Nothing writes the log yet — generation and transmission fill it in later
/// phases — so the rows are seeded through the context against a real case,
/// which is what the log's cascading foreign key requires. The person-condition
/// table is cleared before and after each test, and the cascade from the log to
/// <c>person_conditions</c> takes the seeded rows with it.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class ExportLogEndpointsTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    /// <summary>
    /// A paged read returns the exports newest first by queued time: the later
    /// queued row comes before the earlier one, with the record number, sent
    /// time and outcome each carries.
    /// </summary>
    [Fact]
    public async Task APagedReadReturnsExportsNewestFirst()
    {
        await Clear();

        try
        {
            var report = await Open();
            var queuedAt = new DateTime(2026, 10, 17, 4, 0, 0, DateTimeKind.Utc);

            await Seed(report.Id, "EXP-OLD", queuedAt, generatedMessage: null, sentAt: null, outcome: null);
            await Seed(
                report.Id,
                "EXP-NEW",
                queuedAt.AddHours(1),
                generatedMessage: null,
                sentAt: queuedAt.AddHours(2),
                outcome: "accepted");

            using var response = await api.CreateClient().GetAsync("/api/export-log?pageSize=200");
            response.EnsureSuccessStatusCode();

            var page = (await response.Content
                .ReadFromJsonAsync<PagedResult<CdcExportLogItemResponse>>(Json))!;

            Assert.Equal(2, page.TotalCount);
            Assert.Equal("EXP-NEW", page.Items[0].RecordNumber);
            Assert.Equal("accepted", page.Items[0].Outcome);
            Assert.NotNull(page.Items[0].SentAt);
            Assert.Equal("EXP-OLD", page.Items[1].RecordNumber);
            Assert.Null(page.Items[1].SentAt);
        }
        finally
        {
            await Clear();
        }
    }

    /// <summary>
    /// One export read whole carries the message generated for it, the summary
    /// reading the same as it does in the list.
    /// </summary>
    [Fact]
    public async Task OneExportIsReadWholeWithItsGeneratedMessage()
    {
        await Clear();

        try
        {
            var report = await Open();
            var message = "MSH|^~\\&|SURVSYS|...";

            var id = await Seed(
                report.Id,
                "EXP-ONE",
                new DateTime(2026, 10, 17, 4, 0, 0, DateTimeKind.Utc),
                generatedMessage: message,
                sentAt: null,
                outcome: null);

            using var response = await api.CreateClient().GetAsync($"/api/export-log/{id}");
            response.EnsureSuccessStatusCode();

            var detail = (await response.Content
                .ReadFromJsonAsync<CdcExportLogDetailResponse>(Json))!;

            Assert.Equal(id, detail.Entry.Id);
            Assert.Equal("EXP-ONE", detail.Entry.RecordNumber);
            Assert.Equal(message, detail.GeneratedMessage);
        }
        finally
        {
            await Clear();
        }
    }

    /// <summary>
    /// An id no export has is a 404 rather than an empty body.
    /// </summary>
    [Fact]
    public async Task AMissingExportIsNotFound()
    {
        await Clear();

        using var response = await api.CreateClient().GetAsync("/api/export-log/987654321");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Opens a case about a seeded person for a seeded active condition, and
    /// answers with it — a real case for the log rows' foreign key.
    /// </summary>
    private async Task<PersonConditionResponse> Open()
    {
        var person = await PersonId("Rodriguez, Maria");
        var condition = await ConditionId("Malaria");

        using var response = await api.CreateClient().PostAsJsonAsync(
            "/api/person-conditions",
            new CreatePersonConditionRequest(person, condition),
            Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<PersonConditionResponse>(Json))!;
    }

    /// <summary>Seeds one export-log row against a case and answers with its id.</summary>
    private async Task<int> Seed(
        int personConditionId,
        string recordNumber,
        DateTime queuedAt,
        string? generatedMessage,
        DateTime? sentAt,
        string? outcome)
    {
        await using var context = server.NewContext();

        var entry = new CdcExportLogEntry
        {
            PersonConditionId = personConditionId,
            RecordNumber = recordNumber,
            QueuedAt = queuedAt,
            GeneratedMessage = generatedMessage,
            SentAt = sentAt,
            Outcome = outcome,
        };

        context.CdcExportLog.Add(entry);
        await context.SaveChangesAsync();

        return entry.Id;
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

    /// <summary>
    /// Takes every case out of the table, which cascades to the export log, so
    /// each test starts and ends with an empty log.
    /// </summary>
    private async Task Clear()
    {
        await using var context = server.NewContext();

        await context.PersonConditions.ExecuteDeleteAsync();
    }
}
