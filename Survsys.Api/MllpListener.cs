using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using Survsys.Data;
using Survsys.Messaging;

namespace Survsys.Api;

/// <summary>
/// The inbound ELR intake's MLLP listener, beside the file drop: a background
/// worker that accepts TCP connections from labs, reads each framed message,
/// hands it to <see cref="ElrIntakeService.Receive"/> and answers it with an
/// ACK — <see cref="MllpFraming.Accept"/> when it was queued for review,
/// <see cref="MllpFraming.Error"/> when it was held as an exception, and
/// <see cref="MllpFraming.Reject"/> when it could not be stored.
/// </summary>
/// <remarks>
/// Off unless <c>ElrIntake:MllpPort</c> is configured, as the file drop is off
/// unless its directory is. Each connection is served on its own task and may
/// carry any number of messages in turn; each message is received under the
/// intake's per-message timeout and in its own scope, as a dropped file is.
/// </remarks>
public sealed class MllpListener(
    IMessageParser parser,
    IServiceScopeFactory scopeFactory,
    IOptions<ElrIntakeOptions> options,
    IOptions<ConformanceOptions> conformance,
    ILogger<MllpListener> logger) : BackgroundService
{
    /// <summary>The source recorded on a message received over MLLP.</summary>
    public const string Source = "mllp";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.MllpPort is not { } port || port is <= 0 or > IPEndPoint.MaxPort)
        {
            logger.LogInformation(
                "The MLLP listener is off: no {Section}:{Key} is configured.",
                ElrIntakeOptions.SectionName,
                nameof(ElrIntakeOptions.MllpPort));
            return;
        }

        var messageTimeout = TimeSpan.FromSeconds(Math.Max(1, options.Value.MessageTimeoutSeconds));
        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        logger.LogInformation("The MLLP listener is listening on port {Port}.", port);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(stoppingToken);
                _ = Serve(client, messageTimeout, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// Serves one connection until the lab closes it or the host stops: each
    /// whole frame that arrives is received and answered before the next.
    /// </summary>
    private async Task Serve(TcpClient client, TimeSpan messageTimeout, CancellationToken stoppingToken)
    {
        using var _ = client;
        var remote = client.Client.RemoteEndPoint;

        try
        {
            var stream = client.GetStream();
            var decoder = Encoding.UTF8.GetDecoder();
            var bytes = new byte[8192];
            var chars = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
            var buffer = string.Empty;

            while (true)
            {
                var read = await stream.ReadAsync(bytes, stoppingToken);
                if (read == 0)
                {
                    return;
                }

                buffer += new string(chars, 0, decoder.GetChars(bytes, 0, read, chars, 0));

                while (MllpFraming.TryUnframe(buffer, out var message, out var remainder))
                {
                    buffer = remainder;
                    var ack = await Answer(message, messageTimeout, stoppingToken);
                    await stream.WriteAsync(Encoding.UTF8.GetBytes(MllpFraming.Frame(ack)), stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping; the connection closes with it.
        }
        catch (Exception failure) when (failure is IOException or SocketException)
        {
            logger.LogWarning(failure, "The MLLP connection from {Remote} ended abruptly.", remote);
        }
    }

    /// <summary>Receives one message and answers the ACK it earned.</summary>
    private async Task<string> Answer(string raw, TimeSpan messageTimeout, CancellationToken stoppingToken)
    {
        using var perMessage = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        perMessage.CancelAfter(messageTimeout);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<SurvsysDbContext>();
            var staged = await ElrIntakeService.Receive(
                parser,
                database,
                raw,
                Source,
                perMessage.Token,
                conformance.Value.ProfilesBySender(await ElrIntakeService.TownNames(database, perMessage.Token)));

            var held = staged.FirstOrDefault(message => message.ReviewStatus != MessageReviewStatusRules.EntryCode);
            logger.LogInformation(
                "Staged an MLLP message as {Count} message(s) in {Status}.",
                staged.Count,
                staged[0].ReviewStatus);

            return held is null
                ? MllpFraming.Acknowledgement(raw, MllpFraming.Accept)
                : MllpFraming.Acknowledgement(raw, MllpFraming.Error, held.ExceptionDetail);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception failure)
        {
            logger.LogError(failure, "An MLLP message could not be staged; answering it with a reject.");
            return MllpFraming.Acknowledgement(raw, MllpFraming.Reject, "The message could not be stored.");
        }
    }
}
