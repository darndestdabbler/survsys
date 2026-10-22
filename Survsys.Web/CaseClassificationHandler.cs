using System.Text.Json;
using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// Reads the classification a save answers with, in its
/// <see cref="HeaderName"/> header, into the circuit's
/// <see cref="CaseClassifications"/>.
/// </summary>
/// <remarks>
/// Read through <see cref="CircuitServicesAccessor"/> for the reason
/// <see cref="ApiTokenHandler"/> is: the handler is built outside the
/// circuit's scope. A response with no such header, or one outside a circuit,
/// passes as it came.
/// </remarks>
public sealed class CaseClassificationHandler(CircuitServicesAccessor circuit) : DelegatingHandler
{
    /// <summary>The header the API tier answers a classifying save with.</summary>
    public const string HeaderName = "Case-Classification";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage message,
        CancellationToken cancellationToken)
    {
        var classifications = circuit.Services?.GetService<CaseClassifications>();
        var response = await base.SendAsync(message, cancellationToken);

        if (classifications is not null
            && response.Headers.TryGetValues(HeaderName, out var values)
            && JsonSerializer.Deserialize<CaseClassificationResponse>(values.First(), Json) is { } classification)
        {
            classifications.Record(classification);
        }

        return response;
    }
}
