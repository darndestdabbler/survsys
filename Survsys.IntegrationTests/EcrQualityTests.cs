using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;
using Survsys.Messaging;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The eCR data quality figures against the real host and database, over
/// documents seeded in March 2001, a month no other test receives documents
/// in: an eICR and its RR under a mapped condition, an eICR with no RR, an RR
/// with no eICR under its unmapped code's name, an RR counted under an eICR
/// received before the window, and documents on the test feed and after the
/// window that the production feed's March figures leave out.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class EcrQualityTests(SqlServerFixture server) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string TestCodeSystem = "2.16.840.1.113883.9.9.9.ITEST-QUALITY";

    private const string March = "from=2001-03-01&to=2001-03-31";

    private readonly SurvsysApi api = new(server.ConnectionString);
    private readonly string run = Guid.NewGuid().ToString("N");

    private string Salem => $"itest-salem-{run}";

    private string Hartford => $"itest-hartford-{run}";

    public async Task InitializeAsync()
    {
        await using var context = server.NewContext();

        var malaria = await context.Conditions.Where(condition => condition.Name == "Malaria")
            .Select(condition => condition.Id)
            .SingleAsync();
        context.EcrConditionMappings.Add(
            new EcrConditionMapping { CodeSystem = TestCodeSystem, Code = "M1", ConditionId = malaria });
        context.EcrSenders.Add(new EcrSender { SenderId = Salem, Name = "ITest Salem", Stage = EcrSender.ProductionStage });

        var at = new DateTime(2001, 3, 10, 12, 0, 0, DateTimeKind.Utc);
        context.EcrDocuments.AddRange(
            Eicr("e1", Salem, at, created: at.AddMinutes(-30), errors: 1),
            Rr("r1", "e1", at.AddMinutes(5), "M1", warnings: 1),
            Eicr("e2", Salem, at.AddDays(1), created: at.AddDays(1).AddMinutes(-120)),
            Rr("r3", "missing", at.AddDays(2), "U1"),
            Eicr("e4", Hartford, at.AddDays(2), feed: EcrDocument.TestFeed),
            Rr("r4", "e4", at.AddDays(2), "M1", feed: EcrDocument.TestFeed),
            Eicr("e5", Hartford, new DateTime(2001, 2, 20, 12, 0, 0, DateTimeKind.Utc)),
            Rr("r5", "e5", at.AddDays(3), "M1"),
            Eicr("e6", Salem, new DateTime(2001, 5, 1, 12, 0, 0, DateTimeKind.Utc)));
        await context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using (var context = server.NewContext())
        {
            await context.EcrDocuments.Where(document => document.DocumentId.EndsWith(run)).ExecuteDeleteAsync();
            await context.EcrConditionMappings
                .Where(mapping => mapping.CodeSystem == TestCodeSystem)
                .ExecuteDeleteAsync();
            await context.EcrSenders.Where(sender => sender.SenderId == Salem).ExecuteDeleteAsync();
        }

        api.Dispose();
    }

    [Fact]
    public async Task TheProductionFeedsFiguresAreBySenderAndCondition()
    {
        var rows = await Rows($"/api/ecr-quality?feed={EcrDocument.ProductionFeed}&{March}");

        Assert.Equal(
            [
                new EcrQualityRowResponse(Hartford, null, "Malaria", 0, 1, 0, 0, null, null, 0, 0),
                new EcrQualityRowResponse(Salem, "ITest Salem", "Malaria", 1, 1, 1, 1, 30, 30, 0, 0),
                new EcrQualityRowResponse(Salem, "ITest Salem", null, 1, 0, 0, 0, 120, 120, 1, 0),
                new EcrQualityRowResponse(null, null, "ITest unmapped U1", 0, 1, 0, 0, null, null, 0, 1),
            ],
            rows);
    }

    [Fact]
    public async Task EveryFeedIsCountedWhereNoneIsChosen()
    {
        var hartford = Assert.Single(await Rows($"/api/ecr-quality?{March}"), row => row.Sender == Hartford);

        Assert.Equal((1, 2, 0, 0), (hartford.Eicrs, hartford.Rrs, hartford.EicrsWithoutRr, hartford.RrsWithoutEicr));
    }

    [Fact]
    public async Task TheWindowCountsOnlyTheDocumentsReceivedInIt()
    {
        var rows = await Rows($"/api/ecr-quality?feed={EcrDocument.ProductionFeed}&from=2001-05-01&to=2001-05-01");

        var row = Assert.Single(rows);
        Assert.Equal((Salem, (string?)null, 1, 1), (row.Sender, row.Condition, row.Eicrs, row.EicrsWithoutRr));
    }

    [Fact]
    public async Task AWindowThatEndsBeforeItStartsIsRefused()
    {
        using var refused = await api.CreateClient().GetAsync("/api/ecr-quality?from=2001-03-31&to=2001-03-01");

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
    }

    /// <summary>The rows the endpoint answers, narrowed to this test's own senders and conditions.</summary>
    private async Task<List<EcrQualityRowResponse>> Rows(string url)
    {
        var rows = await api.CreateClient().GetFromJsonAsync<List<EcrQualityRowResponse>>(url, Json);

        return rows!
            .Where(row => row.Sender == Salem || row.Sender == Hartford
                || (row.Sender is null && row.Condition == "ITest unmapped U1"))
            .ToList();
    }

    private EcrDocument Eicr(
        string id,
        string sender,
        DateTime received,
        DateTime? created = null,
        int errors = 0,
        string feed = EcrDocument.ProductionFeed) => new()
    {
        DocumentId = $"{id}-{run}",
        Kind = EcrDocument.EicrKind,
        Feed = feed,
        Sender = sender,
        RawXml = "<ClinicalDocument xmlns=\"urn:hl7-org:v3\" />",
        ReceivedAt = received,
        CreatedAt = created,
        Findings = Enumerable.Range(0, errors)
            .Select(_ => new EcrValidationFinding
            {
                Source = CdaValidator.SchemaSource, Severity = CdaValidator.Error, Message = "An error.",
            })
            .ToList(),
    };

    private EcrDocument Rr(
        string id,
        string eicr,
        DateTime received,
        string code,
        int warnings = 0,
        string feed = EcrDocument.ProductionFeed) => new()
    {
        DocumentId = $"{id}-{run}",
        Kind = EcrDocument.RrKind,
        Feed = feed,
        Sender = "APHL",
        EicrDocumentId = $"{eicr}-{run}",
        RawXml = "<ClinicalDocument xmlns=\"urn:hl7-org:v3\" />",
        ReceivedAt = received,
        Findings = Enumerable.Range(0, warnings)
            .Select(_ => new EcrValidationFinding
            {
                Source = CdaValidator.SchematronSource, Severity = CdaValidator.Warning, Message = "A warning.",
            })
            .ToList(),
        RrConditions =
        [
            new EcrRrCondition
            {
                CodeSystem = TestCodeSystem,
                Code = code,
                DisplayName = $"ITest unmapped {code}",
                DeterminationCode = "RRVS1",
            },
        ],
    };
}
