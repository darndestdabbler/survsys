using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
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
/// rendered beside the RR's conditions and both documents' findings; and each
/// determination code's routing, from intake to the queue's review filter and
/// the document's conditions.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class EcrEndpointsTests(SqlServerFixture server) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The responsible agency the routing test configures as this jurisdiction.</summary>
    private const string Agency = "2.16.840.1.113883.4.6^12341234";

    /// <summary>The code system the routing test's condition codes and their mappings are in.</summary>
    private const string TestCodeSystem = "2.16.840.1.113883.9.9.9.ITEST-ECR";

    private readonly SurvsysApi api = new(server.ConnectionString);
    private readonly string eicrId = $"itest-eicr-{Guid.NewGuid():N}";
    private readonly string rrId = $"itest-rr-{Guid.NewGuid():N}";
    private readonly string secondRrId = $"itest-rr2-{Guid.NewGuid():N}";

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using (var context = server.NewContext())
        {
            await context.EcrDocuments
                .Where(document => document.DocumentId == eicrId
                    || document.DocumentId == rrId
                    || document.DocumentId == secondRrId)
                .ExecuteDeleteAsync();
            await context.EcrConditionMappings
                .Where(mapping => mapping.CodeSystem == TestCodeSystem)
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
    public async Task AnEicrWithTwoRrsNamesTheDocumentOfEachFinding()
    {
        var (eicr, _) = await StorePair();
        await using (var context = server.NewContext())
        {
            var second = Rr(secondRrId, DateTime.UtcNow.AddSeconds(2));
            second.Findings.Add(new()
            {
                Source = CdaValidator.SchematronSource, Severity = CdaValidator.Fatal, Message = "No encounter.",
            });
            context.EcrDocuments.Add(second);
            await context.SaveChangesAsync();
        }

        var detail = await api.CreateClient()
            .GetFromJsonAsync<EcrDocumentDetailResponse>($"/api/ecr-documents/{eicr}", Json);

        Assert.Equal([rrId, secondRrId], detail!.Conditions.Select(condition => condition.RrDocumentId));
        Assert.Equal(
            [(EcrDocument.EicrKind, eicrId), (EcrDocument.EicrKind, eicrId), (EcrDocument.RrKind, secondRrId)],
            detail.Findings.Select(finding => (finding.DocumentKind, finding.DocumentId)));
        Assert.Equal(CdaValidator.Fatal, detail.Findings[2].Severity);
    }

    [Fact]
    public async Task EachDeterminationCodeIsRoutedFromIntakeToTheQueue()
    {
        var (eicr, _) = await StorePair();
        await using (var context = server.NewContext())
        {
            await context.EcrDocuments.Where(document => document.DocumentId == rrId).ExecuteDeleteAsync();

            var malaria = await context.Conditions
                .Where(condition => condition.Name == "Malaria")
                .Select(condition => condition.Id)
                .SingleAsync();
            context.EcrConditionMappings.AddRange(
                new EcrConditionMapping { CodeSystem = TestCodeSystem, Code = "reportable-mapped", ConditionId = malaria },
                new EcrConditionMapping { CodeSystem = TestCodeSystem, Code = "may-be-mapped", ConditionId = malaria },
                new EcrConditionMapping { CodeSystem = TestCodeSystem, Code = "reportable-listed", ConditionId = null });

            var raw = RoutedRr(
                ("reportable-mapped", EcrReportability.Reportable, Agency),
                ("reportable-listed", EcrReportability.Reportable, Agency),
                ("reportable-absent", EcrReportability.Reportable, Agency),
                ("may-be-mapped", EcrReportability.MayBeReportable, Agency),
                ("not-reportable", EcrReportability.NotReportable, Agency),
                ("no-rule-met", EcrReportability.NoRuleMet, Agency),
                ("another-agency", EcrReportability.Reportable, "2.16.840.1.113883.4.6^999"));
            context.EcrDocuments.Add(
                EcrIntakeService.Stage(new CdaParser(), new CdaValidator(), raw, EcrDocument.TestFeed, null, [Agency]));
            await context.SaveChangesAsync();

            var kept = await context.EcrRrConditions
                .Where(condition => condition.EcrDocument!.DocumentId == rrId)
                .OrderBy(condition => condition.Id)
                .Select(condition => condition.Code)
                .ToListAsync();
            Assert.Equal(["reportable-mapped", "reportable-listed", "reportable-absent", "may-be-mapped"], kept);
        }

        using var routed = api.WithWebHostBuilder(builder =>
            builder.UseSetting("EcrIntake:ResponsibleAgencyIds:0", Agency));
        var client = routed.CreateClient();

        var inReview = await client.GetFromJsonAsync<PagedResult<EcrDocumentResponse>>(
            $"/api/ecr-documents?feed={EcrDocument.TestFeed}&review=true&pageSize={Paging.MaxPageSize}", Json);
        var rr = Assert.Single(inReview!.Items, row => row.DocumentId == rrId);
        Assert.Equal(3, rr.ReviewCount);
        Assert.DoesNotContain(inReview.Items, row => row.Id == eicr);
        Assert.All(inReview.Items, row => Assert.True(row.ReviewCount > 0));

        var detail = await client.GetFromJsonAsync<EcrDocumentDetailResponse>($"/api/ecr-documents/{rr.Id}", Json);
        Assert.Equal(
            [
                ("reportable-mapped", "Malaria", EcrReportability.ReportableRouting),
                ("reportable-listed", null, EcrReportability.ReviewRouting),
                ("reportable-absent", null, EcrReportability.ReviewRouting),
                ("may-be-mapped", "Malaria", EcrReportability.ReviewRouting),
                ("not-reportable", null, null),
                ("no-rule-met", null, null),
                ("another-agency", null, null),
            ],
            detail!.Conditions.Select(condition =>
                (condition.ConditionCode!, condition.MappedConditionName, condition.Routing)));
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

        var rr = Rr(rrId, received.AddSeconds(1));

        context.EcrDocuments.AddRange(eicr, rr);
        await context.SaveChangesAsync();
        return (eicr.Id, rr.Id);
    }

    /// <summary>
    /// An RR to the eICR, numbered <c>rrId</c>, with one condition in the test
    /// code system for each code, determination and responsible agency given.
    /// </summary>
    private string RoutedRr(params (string Code, string Determination, string Agency)[] conditions)
    {
        var entries = conditions.Select(condition =>
        {
            var agency = condition.Agency.Split('^');
            return $"""
                <entry><observation>
                  <templateId root="2.16.840.1.113883.10.20.15.2.3.12" />
                  <value code="{condition.Code}" codeSystem="{TestCodeSystem}" displayName="{condition.Code}" />
                  <entryRelationship><organizer>
                    <templateId root="2.16.840.1.113883.10.20.15.2.3.13" />
                    <code code="RRVS7" />
                    <participant>
                      <templateId root="2.16.840.1.113883.10.20.15.2.4.2" />
                      <participantRole><id root="{agency[0]}" extension="{agency[1]}" /></participantRole>
                    </participant>
                    <component><observation>
                      <templateId root="2.16.840.1.113883.10.20.15.2.3.19" />
                      <value code="{condition.Determination}" />
                    </observation></component>
                  </organizer></entryRelationship>
                </observation></entry>
                """;
        });

        return $"""
            <ClinicalDocument xmlns="urn:hl7-org:v3">
              <templateId root="2.16.840.1.113883.10.20.15.2.1.2" extension="2017-04-01" />
              <id root="{rrId}" />
              <component><structuredBody><component><section>
                <entry><act>
                  <templateId root="2.16.840.1.113883.10.20.15.2.3.10" />
                  <id root="{eicrId}" />
                </act></entry>
                {string.Concat(entries)}
              </section></component></structuredBody></component>
            </ClinicalDocument>
            """;
    }

    /// <summary>An RR to the eICR, finding Pertussis reportable to the State Department of Health.</summary>
    private EcrDocument Rr(string id, DateTime received) => new()
    {
        DocumentId = id,
        Kind = EcrDocument.RrKind,
        Feed = EcrDocument.TestFeed,
        EicrDocumentId = eicrId,
        RawXml = $"""
            <ClinicalDocument xmlns="urn:hl7-org:v3">
              <templateId root="2.16.840.1.113883.10.20.15.2.1.2" extension="2017-04-01" />
              <id root="{id}" />
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
        ReceivedAt = received,
    };
}
