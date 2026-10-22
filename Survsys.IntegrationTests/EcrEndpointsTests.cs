using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Survsys.Messaging;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The eCR queue and document screen's reads against the real host and
/// database: the queue lists a feed's documents newest first with their
/// finding counts, and an eICR and its RR each read as the pair, the eICR
/// rendered beside the RR's conditions and both documents' findings.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class EcrEndpointsTests(SqlServerFixture server) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);
    private readonly string eicrId = $"itest-eicr-{Guid.NewGuid():N}";
    private readonly string rrId = $"itest-rr-{Guid.NewGuid():N}";

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using (var context = server.NewContext())
        {
            await context.EcrDocuments
                .Where(document => document.DocumentId == eicrId || document.DocumentId == rrId)
                .ExecuteDeleteAsync();
        }

        api.Dispose();
    }

    [Fact]
    public async Task TheQueueListsAFeedsDocumentsNewestFirstWithTheirFindingCounts()
    {
        var (eicr, rr) = await StorePair();
        var client = api.CreateClient();

        var page = await client.GetFromJsonAsync<PagedResult<EcrDocumentResponse>>(
            $"/api/ecr-documents?feed={EcrDocument.TestFeed}&pageSize={Paging.MaxPageSize}", Json);

        var rows = page!.Items.Where(row => row.Id == eicr || row.Id == rr).ToList();
        Assert.Equal([rr, eicr], rows.Select(row => row.Id));
        Assert.Equal((0, 0), (rows[0].ErrorCount, rows[0].WarningCount));
        Assert.Equal((1, 1), (rows[1].ErrorCount, rows[1].WarningCount));
        Assert.All(page.Items, row => Assert.Equal(EcrDocument.TestFeed, row.Feed));

        var production = await client.GetFromJsonAsync<PagedResult<EcrDocumentResponse>>(
            $"/api/ecr-documents?feed={EcrDocument.ProductionFeed}&pageSize={Paging.MaxPageSize}", Json);
        Assert.DoesNotContain(production!.Items, row => row.Id == eicr || row.Id == rr);
    }

    [Fact]
    public async Task AnEicrAndItsRrEachReadAsThePair()
    {
        var (eicr, rr) = await StorePair();
        var client = api.CreateClient();

        foreach (var id in new[] { eicr, rr })
        {
            var detail = await client.GetFromJsonAsync<EcrDocumentDetailResponse>($"/api/ecr-documents/{id}", Json);

            Assert.Equal(id, detail!.Document.Id);
            Assert.Contains("Integration eICR", detail.EicrHtml, StringComparison.Ordinal);
            var condition = Assert.Single(detail.Conditions);
            Assert.Equal(rrId, condition.RrDocumentId);
            Assert.Equal("27836007", condition.ConditionCode);
            Assert.Equal("RRVS1", condition.DeterminationCode);
            Assert.Equal("State Department of Health", condition.ResponsibleAgencyName);
            Assert.Equal(
                [CdaValidator.Error, CdaValidator.Warning],
                detail.Findings.Select(finding => finding.Severity));
            Assert.All(detail.Findings, finding => Assert.Equal(eicrId, finding.DocumentId));
        }
    }

    [Fact]
    public async Task AnUnknownDocumentIsNotFound()
    {
        using var response = await api.CreateClient().GetAsync("/api/ecr-documents/2147483647");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>An eICR with an error and a warning, and an RR to it received after it, with none.</summary>
    private async Task<(int Eicr, int Rr)> StorePair()
    {
        await using var context = server.NewContext();
        var received = DateTime.UtcNow;

        var eicr = new EcrDocument
        {
            DocumentId = eicrId,
            Kind = EcrDocument.EicrKind,
            Feed = EcrDocument.TestFeed,
            RawXml = $"""
                <ClinicalDocument xmlns="urn:hl7-org:v3">
                  <templateId root="2.16.840.1.113883.10.20.15.2" extension="2016-12-01" />
                  <id root="{eicrId}" />
                  <title>Integration eICR</title>
                </ClinicalDocument>
                """,
            ReceivedAt = received,
            Findings =
            [
                new() { Source = CdaValidator.SchemaSource, Severity = CdaValidator.Error, Message = "No effectiveTime." },
                new() { Source = CdaValidator.SchematronSource, Severity = CdaValidator.Warning, Message = "No setId." },
            ],
        };

        var rr = new EcrDocument
        {
            DocumentId = rrId,
            Kind = EcrDocument.RrKind,
            Feed = EcrDocument.TestFeed,
            EicrDocumentId = eicrId,
            RawXml = $"""
                <ClinicalDocument xmlns="urn:hl7-org:v3">
                  <templateId root="2.16.840.1.113883.10.20.15.2.1.2" extension="2017-04-01" />
                  <id root="{rrId}" />
                  <component><structuredBody><component><section>
                    <entry><act>
                      <templateId root="2.16.840.1.113883.10.20.15.2.3.10" />
                      <id root="{eicrId}" />
                    </act></entry>
                    <entry><observation>
                      <templateId root="2.16.840.1.113883.10.20.15.2.3.12" />
                      <value code="27836007" displayName="Pertussis (disorder)" />
                      <entryRelationship><organizer>
                        <templateId root="2.16.840.1.113883.10.20.15.2.3.13" />
                        <code code="RRVS7" />
                        <participant>
                          <templateId root="2.16.840.1.113883.10.20.15.2.4.2" />
                          <participantRole>
                            <id root="2.16.840.1.113883.4.6" extension="12341234" />
                            <playingEntity><name>State Department of Health</name></playingEntity>
                          </participantRole>
                        </participant>
                        <component><observation>
                          <templateId root="2.16.840.1.113883.10.20.15.2.3.19" />
                          <value code="RRVS1" displayName="Reportable" />
                        </observation></component>
                      </organizer></entryRelationship>
                    </observation></entry>
                  </section></component></structuredBody></component>
                </ClinicalDocument>
                """,
            ReceivedAt = received.AddSeconds(1),
        };

        context.EcrDocuments.AddRange(eicr, rr);
        await context.SaveChangesAsync();
        return (eicr.Id, rr.Id);
    }
}
