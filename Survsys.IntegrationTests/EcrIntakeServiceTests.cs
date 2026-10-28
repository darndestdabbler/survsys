using Microsoft.EntityFrameworkCore;
using Survsys.Api;
using Survsys.Data;
using Survsys.Messaging;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The eCR intake against the database: a document is stored with its raw
/// XML unchanged, and the same document received again is not stored twice.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class EcrIntakeServiceTests(SqlServerFixture server)
{
    [Fact]
    public async Task ReceiveStoresADocumentOnceByItsId()
    {
        var documentId = $"itest-{Guid.NewGuid():N}";
        var raw = $"""
            <ClinicalDocument xmlns="urn:hl7-org:v3">
              <templateId root="2.16.840.1.113883.10.20.15.2" extension="2016-12-01" />
              <id root="{documentId}" />
            </ClinicalDocument>
            """;
        var parser = new CdaParser();

        try
        {
            await using (var context = server.NewContext())
            {
                var stored = await EcrIntakeService.Receive(
                    parser, new CdaValidator(), context, raw, EcrDocument.TestFeed, "first.xml", [], CancellationToken.None);
                Assert.NotNull(stored);
            }

            await using (var context = server.NewContext())
            {
                var again = await EcrIntakeService.Receive(
                    parser, new CdaValidator(), context, raw, EcrDocument.ProductionFeed, "again.xml", [], CancellationToken.None);
                Assert.Null(again);
            }

            await using (var context = server.NewContext())
            {
                var document = await context.EcrDocuments.SingleAsync(document => document.DocumentId == documentId);
                Assert.Equal(raw, document.RawXml);
                Assert.Equal(EcrDocument.EicrKind, document.Kind);
                Assert.Equal(EcrDocument.TestFeed, document.Feed);
                Assert.Equal("first.xml", document.Source);
            }
        }
        finally
        {
            await using var context = server.NewContext();
            await context.EcrDocuments.Where(document => document.DocumentId == documentId).ExecuteDeleteAsync();
        }
    }
}
