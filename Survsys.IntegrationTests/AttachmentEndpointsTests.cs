using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The attachment slice's correction and removal, against the real database: an
/// attachment refiled under another category and an attachment deleted with its
/// stored file, each change audited.
/// </summary>
/// <remarks>
/// The sibling of <see cref="LabEndpointsTests"/>, built the same way: the
/// report is <c>Fixtures/person-conditions.sql</c>'s, re-seeded if a report
/// test in this collection has cleared it, and every attachment this test
/// uploads is cleared afterwards so a report with attachments against it never
/// refuses those tests' delete. The suite does not skip when SQL Server is
/// absent; <see cref="SqlServerFixture"/> fails it loudly instead.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class AttachmentEndpointsTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task AnAttachmentIsRefiledAndItsFileIsLeftUntouchedAndAudited()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        try
        {
            var stored = await Upload(client, reportId, "lab", bytes: [1, 2, 3, 4, 5]);

            using var put = await client.PutAsJsonAsync($"/api/attachments/{stored.Id}",
                new UpdateAttachmentRequest("letter"), Json);

            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            var updated = (await put.Content.ReadFromJsonAsync<AttachmentResponse>(Json))!;

            // Refiled, and nothing else about the attachment moved.
            Assert.Equal("letter", updated.Category);
            Assert.Equal(stored.Filename, updated.Filename);
            Assert.Equal(stored.ContentType, updated.ContentType);
            Assert.Equal(stored.Size, updated.Size);

            // The bytes are still there, under the content type and name they went in as.
            using var content = await client.GetAsync($"/api/attachments/{stored.Id}/content");
            content.EnsureSuccessStatusCode();
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, await content.Content.ReadAsByteArrayAsync());

            Assert.Contains("Update", await AuditOperations("attachments", stored.Id));
        }
        finally
        {
            await ClearAttachmentsOf(reportId);
        }
    }

    [Fact]
    public async Task ARefileToACategoryNotOnTheListIsRefused()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        try
        {
            var stored = await Upload(client, reportId, "lab");

            using var put = await client.PutAsJsonAsync($"/api/attachments/{stored.Id}",
                new UpdateAttachmentRequest("radiology"), Json);

            Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
            Assert.Equal("application/problem+json", put.Content.Headers.ContentType?.MediaType);
            using var problem = JsonDocument.Parse(await put.Content.ReadAsStringAsync());
            Assert.Contains("category 'radiology' is not one",
                problem.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
        }
        finally
        {
            await ClearAttachmentsOf(reportId);
        }
    }

    [Fact]
    public async Task AnUploadIsFiledUnderACategoryOnTheListAndRefusedUnderOneThatIsNot()
    {
        const string added = "itest-photo";
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        try
        {
            using (var created = await client.PostAsJsonAsync(
                $"/api/codes/{CodeLists.AttachmentCategory}", new SaveCodeRequest(added, "Photo"), Json))
            {
                Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            }

            Assert.Equal("letter", (await Upload(client, reportId, "letter")).Category);
            Assert.Equal(added, (await Upload(client, reportId, added)).Category);

            using var form = new MultipartFormDataContent();
            var file = new ByteArrayContent([1, 2, 3]);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(file, "file", "scan.pdf");
            form.Add(new StringContent("radiology"), "category");

            using var refused = await client.PostAsync($"/api/person-conditions/{reportId}/attachments", form);
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        }
        finally
        {
            await ClearAttachmentsOf(reportId);

            await using var context = server.NewContext();
            await context.CodeValues
                .Where(code => code.List == CodeLists.AttachmentCategory && code.Code == added)
                .ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task AnAttachmentAndItsFileAreDeletedTogetherAndAudited()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        var stored = await Upload(client, reportId, "lab");

        int fileId;
        await using (var context = server.NewContext())
        {
            fileId = await context.Attachments
                .Where(attachment => attachment.Id == stored.Id)
                .Select(attachment => attachment.AttachmentFileId)
                .SingleAsync();
        }

        using var delete = await client.DeleteAsync($"/api/attachments/{stored.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        // The metadata is gone, and so are the bytes it pointed at.
        using var contentGone = await client.GetAsync($"/api/attachments/{stored.Id}/content");
        Assert.Equal(HttpStatusCode.NotFound, contentGone.StatusCode);

        await using (var context = server.NewContext())
        {
            Assert.False(await context.Attachments.AnyAsync(a => a.Id == stored.Id));
            Assert.False(await context.AttachmentFiles.AnyAsync(f => f.Id == fileId));
        }

        Assert.Contains("Delete", await AuditOperations("attachments", stored.Id));
        Assert.Contains("Delete", await AuditOperations("attachment_files", fileId));
    }

    /// <summary>
    /// What the Attachments panel's file name and Delete ask for: the file as
    /// it was attached, under its type and name, and then gone.
    /// </summary>
    [Fact]
    public async Task AnAttachmentIsDownloadedAsItWasAttachedAndThenDeleted()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        try
        {
            var stored = await Upload(client, reportId, "letter", "exposure-letter.txt", "text/plain", [72, 105, 10]);

            using (var content = await client.GetAsync($"/api/attachments/{stored.Id}/content"))
            {
                Assert.Equal(HttpStatusCode.OK, content.StatusCode);
                Assert.Equal(new byte[] { 72, 105, 10 }, await content.Content.ReadAsByteArrayAsync());
                Assert.Equal("text/plain", content.Content.Headers.ContentType?.MediaType);
                Assert.Equal("exposure-letter.txt", content.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
            }

            using var delete = await client.DeleteAsync($"/api/attachments/{stored.Id}");
            Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

            using var gone = await client.GetAsync($"/api/attachments/{stored.Id}/content");
            Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        }
        finally
        {
            await ClearAttachmentsOf(reportId);
        }
    }

    /// <summary>
    /// A file attached to an outbreak is listed under it and under no report,
    /// downloads as stored, and goes with its file when the outbreak is
    /// deleted; an outbreak nobody has is a 404 to both routes.
    /// </summary>
    [Fact]
    public async Task AFileAttachedToAnOutbreakIsListedUnderItAndGoesWithIt()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        using var created = await client.PostAsJsonAsync(
            "/api/outbreaks", new SaveOutbreakRequest("Attachment outbreak"), Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var outbreakId = (await created.Content.ReadFromJsonAsync<OutbreakDetailResponse>(Json))!.Id;

        var stored = await Upload(client, outbreakId, "lab", bytes: [9, 8, 7],
            route: $"/api/outbreaks/{outbreakId}/attachments");
        Assert.Equal(outbreakId, stored.OutbreakId);
        Assert.Null(stored.PersonConditionId);

        var listed = (await client.GetFromJsonAsync<PagedResult<AttachmentResponse>>(
            $"/api/outbreaks/{outbreakId}/attachments", Json))!;
        Assert.Equal([stored.Id], listed.Items.Select(attachment => attachment.Id));

        var onReport = (await client.GetFromJsonAsync<PagedResult<AttachmentResponse>>(
            $"/api/person-conditions/{reportId}/attachments?pageSize=100", Json))!;
        Assert.DoesNotContain(onReport.Items, attachment => attachment.Id == stored.Id);

        Assert.Equal([9, 8, 7], await client.GetByteArrayAsync($"/api/attachments/{stored.Id}/content"));

        using (var deleted = await client.DeleteAsync($"/api/outbreaks/{outbreakId}"))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        }

        await using (var context = server.NewContext())
        {
            Assert.False(await context.Attachments.AnyAsync(attachment => attachment.Id == stored.Id));
        }

        using var missingList = await client.GetAsync("/api/outbreaks/987654/attachments");
        Assert.Equal(HttpStatusCode.NotFound, missingList.StatusCode);
    }

    [Fact]
    public async Task AnEditOrDeleteOfAnAttachmentNobodyHasIsA404()
    {
        var client = api.CreateClient();

        using var put = await client.PutAsJsonAsync("/api/attachments/987654",
            new UpdateAttachmentRequest("letter"), Json);
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);

        using var delete = await client.DeleteAsync("/api/attachments/987654");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    /// <summary>
    /// Uploads one file to a report — or to the route given — and answers with
    /// the attachment as stored, through the multipart form the upload route
    /// takes.
    /// </summary>
    private async Task<AttachmentResponse> Upload(
        HttpClient client,
        int reportId,
        string category,
        string filename = "culture-result.pdf",
        string contentType = "application/pdf",
        byte[]? bytes = null,
        string? route = null)
    {
        using var form = new MultipartFormDataContent();

        var file = new ByteArrayContent(bytes ?? [1, 2, 3, 4]);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", filename);
        form.Add(new StringContent(category), "category");

        using var response = await client.PostAsync(
            route ?? $"/api/person-conditions/{reportId}/attachments", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<AttachmentResponse>(Json))!;
    }

    /// <summary>
    /// The operations audited against a table for one single-column key, in the
    /// order they were written, read back through a fresh context.
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

    /// <summary>
    /// The seeded report's id, re-seeding the report if a report test in this
    /// collection has cleared it since the fixture was built — the helper
    /// <see cref="LabEndpointsTests"/> keeps for the same reason.
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
    /// Takes every attachment this test uploaded against the report out of the
    /// database — the metadata first, then the files it pointed at — and leaves
    /// the report, so a report test's clear is not refused by an attachment
    /// still scoped to it.
    /// </summary>
    private async Task ClearAttachmentsOf(int reportId)
    {
        await using var context = server.NewContext();

        var attachments = await context.Attachments
            .Where(attachment => attachment.PersonConditionId == reportId)
            .Select(attachment => new { attachment.Id, attachment.AttachmentFileId })
            .ToListAsync();

        if (attachments.Count == 0)
        {
            return;
        }

        var ids = attachments.Select(attachment => attachment.Id).ToList();
        await context.Attachments.Where(attachment => ids.Contains(attachment.Id)).ExecuteDeleteAsync();

        var fileIds = attachments.Select(attachment => attachment.AttachmentFileId).Distinct().ToList();
        await context.AttachmentFiles.Where(file => fileIds.Contains(file.Id)).ExecuteDeleteAsync();
    }
}
