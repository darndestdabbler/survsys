using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The eCR sender endpoints against the real database: a sender is added,
/// saved over, listed, refused where its id is already on file or it is not
/// one to save, and deleted; and the senders whose results are written as
/// labs are those with the setting set.
/// </summary>
/// <remarks>
/// Every sender these tests write has an id of this run's own, deleted in a
/// finally, so the suite's shared database is left as it was.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class EcrSenderEndpointsTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);
    private readonly string run = Guid.NewGuid().ToString("N");

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task ASenderIsAddedSavedListedAndDeleted()
    {
        var client = api.CreateClient();
        var senderId = $"itest-sender-{run}";

        try
        {
            using var created = await client.PostAsJsonAsync(
                "/api/ecr-senders", new SaveEcrSenderRequest($" {senderId} ", " ITest Salem ", EcrSender.TestStage));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var sender = (await created.Content.ReadFromJsonAsync<EcrSenderResponse>(Json))!;
            Assert.Equal(new EcrSenderResponse(sender.Id, senderId, "ITest Salem", EcrSender.TestStage, false), sender);

            using var taken = await client.PostAsJsonAsync(
                "/api/ecr-senders", new SaveEcrSenderRequest(senderId, "Another", EcrSender.TestStage));
            Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);

            using var unknownStage = await client.PutAsJsonAsync(
                $"/api/ecr-senders/{sender.Id}", new SaveEcrSenderRequest(senderId, "ITest Salem", "live"));
            Assert.Equal(HttpStatusCode.BadRequest, unknownStage.StatusCode);

            using var saved = await client.PutAsJsonAsync(
                $"/api/ecr-senders/{sender.Id}",
                new SaveEcrSenderRequest(senderId, "ITest Salem", EcrSender.ProductionStage, true));
            saved.EnsureSuccessStatusCode();

            var listed = Assert.Single(
                (await client.GetFromJsonAsync<List<EcrSenderResponse>>("/api/ecr-senders", Json))!,
                row => row.SenderId == senderId);
            Assert.Equal(new EcrSenderResponse(sender.Id, senderId, "ITest Salem", EcrSender.ProductionStage, true), listed);

            using var noSuch = await client.PutAsJsonAsync(
                $"/api/ecr-senders/{int.MaxValue}", new SaveEcrSenderRequest($"{senderId}-x", "X", EcrSender.TestStage));
            Assert.Equal(HttpStatusCode.NotFound, noSuch.StatusCode);

            using var deleted = await client.DeleteAsync($"/api/ecr-senders/{sender.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            using var again = await client.DeleteAsync($"/api/ecr-senders/{sender.Id}");
            Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        }
        finally
        {
            await ClearTestSenders();
        }
    }

    [Fact]
    public async Task TheLabSendersAreThoseWhoseResultsAreTakenAsLabs()
    {
        try
        {
            await using (var context = server.NewContext())
            {
                context.EcrSenders.AddRange(
                    new EcrSender { SenderId = $"itest-labs-{run}", Name = "Labs", TakesLabResults = true },
                    new EcrSender { SenderId = $"itest-no-labs-{run}", Name = "No labs" });
                await context.SaveChangesAsync();
            }

            await using var reading = server.NewContext();
            var labSenders = await EcrIntakeService.LabSenders(reading, CancellationToken.None);

            Assert.Contains($"itest-labs-{run}", labSenders);
            Assert.DoesNotContain($"itest-no-labs-{run}", labSenders);
        }
        finally
        {
            await ClearTestSenders();
        }
    }

    private async Task ClearTestSenders()
    {
        await using var context = server.NewContext();
        await context.EcrSenders.Where(sender => sender.SenderId.EndsWith(run)).ExecuteDeleteAsync();
    }
}
