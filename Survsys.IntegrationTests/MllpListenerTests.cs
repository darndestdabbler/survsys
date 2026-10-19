using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Survsys.Api;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The MLLP listener end to end, against the real host and database: a framed
/// message from a sending facility on file answered AA and queued for review,
/// and one from a sender not on file answered AE and held as an exception.
/// </summary>
/// <remarks>
/// The host is the real <c>Program.cs</c>, with its listener turned on through
/// <c>ElrIntake:MllpPort</c> — set in the environment before the host builds,
/// as <see cref="ElrIntakeTests"/> sets its drop directory — on a port free
/// when the test asked for one. Each message carries a control id of its own,
/// so its staged row is found by the raw text rather than by order of arrival.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class MllpListenerTests(SqlServerFixture server)
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task AKnownSenderIsAcceptedAndQueuedAndAnUnknownOneIsHeldAsAnException()
    {
        var port = FreePort();
        Environment.SetEnvironmentVariable("ElrIntake__MllpPort", port.ToString());

        var senderId = await EnsureSendingFacility("TEST_LAB");

        // The vocabulary and rules that translate the fixture message, so a
        // known sender's message stays queued for review.
        var vocabulary = await IntakeVocabulary.SeedAccepting(server, "TEST_LAB");

        try
        {
            using var api = new SurvsysApi(server.ConnectionString);
            using var _ = api.CreateClient();

            var fixture = await File.ReadAllTextAsync(
                Path.Combine(AppContext.BaseDirectory, "Fixtures", "Hl7", "oru-r01-chlamydia.hl7"));

            using var connection = await Connect(port);
            var stream = connection.GetStream();

            var knownId = "MLLP" + Guid.NewGuid().ToString("N")[..16];
            var ack = await Send(stream, fixture.Replace("|MSG00001|", $"|{knownId}|", StringComparison.Ordinal));
            Assert.Equal($"MSA|AA|{knownId}", Msa(ack));

            var queued = await StagedWith(knownId);
            Assert.Equal(MllpListener.Source, queued.Source);
            Assert.Equal(MessageReviewStatusRules.EntryCode, queued.ReviewStatus);

            var unknownId = "MLLP" + Guid.NewGuid().ToString("N")[..16];
            var nak = await Send(
                stream,
                fixture
                    .Replace("|MSG00001|", $"|{unknownId}|", StringComparison.Ordinal)
                    .Replace("|TEST_LAB^2.16", "|UNKNOWN_LAB^2.16", StringComparison.Ordinal));
            Assert.StartsWith($"MSA|AE|{unknownId}|", Msa(nak));
            Assert.Contains("UNKNOWN_LAB", Msa(nak));

            var held = await StagedWith(unknownId);
            Assert.Equal(MessageReviewStatusRules.ExceptionCode, held.ReviewStatus);
            Assert.Contains("UNKNOWN_LAB", held.ExceptionDetail);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ElrIntake__MllpPort", null);
            await vocabulary.DisposeAsync();
            await RemoveSendingFacility(senderId);
        }
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    /// <summary>Connects to the listener, retrying with a bounded patience while the host starts it.</summary>
    private static async Task<TcpClient> Connect(int port)
    {
        var deadline = DateTime.UtcNow + Patience;

        while (true)
        {
            var client = new TcpClient();
            try
            {
                await client.ConnectAsync(IPAddress.Loopback, port);
                return client;
            }
            catch (SocketException) when (DateTime.UtcNow < deadline)
            {
                client.Dispose();
                await Task.Delay(250);
            }
        }
    }

    /// <summary>Sends one framed message and reads the framed answer, within the patience.</summary>
    private static async Task<string> Send(NetworkStream stream, string message)
    {
        using var timeout = new CancellationTokenSource(Patience);
        await stream.WriteAsync(Encoding.UTF8.GetBytes(MllpFraming.Frame(message)), timeout.Token);

        var received = string.Empty;
        var bytes = new byte[4096];

        while (true)
        {
            var read = await stream.ReadAsync(bytes, timeout.Token);
            Assert.NotEqual(0, read);
            received += Encoding.UTF8.GetString(bytes, 0, read);

            if (MllpFraming.TryUnframe(received, out var answer, out _))
            {
                return answer;
            }
        }
    }

    private static string Msa(string ack) =>
        ack.Split('\r').Single(segment => segment.StartsWith("MSA|", StringComparison.Ordinal));

    private async Task<StagedMessage> StagedWith(string controlId)
    {
        await using var context = server.NewContext();

        return await context.StagedMessages
            .AsNoTracking()
            .SingleAsync(message => message.RawMessage.Contains(controlId));
    }

    private async Task<int?> EnsureSendingFacility(string shortName)
    {
        await using var context = server.NewContext();

        if (await context.Facilities.AnyAsync(facility => facility.ShortName == shortName))
        {
            return null;
        }

        var facility = new Facility { Name = "MLLP test lab", ShortName = shortName, AgencyId = 1 };
        context.Facilities.Add(facility);
        await context.SaveChangesAsync();

        return facility.Id;
    }

    private async Task RemoveSendingFacility(int? facilityId)
    {
        if (facilityId is null)
        {
            return;
        }

        await using var context = server.NewContext();
        await context.Facilities.Where(facility => facility.Id == facilityId).ExecuteDeleteAsync();
    }
}
