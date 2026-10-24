using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// What the address geocoding reads from configuration: whether it is on,
/// each service's endpoint and timeout, and Google's API key.
/// </summary>
/// <remarks>
/// Off by default, so a deployment that has not turned it on sends nothing
/// anywhere. The endpoints default to the public services and are settings so
/// a test can point them at a local stub. The key is not bound from the
/// section: <c>Program.cs</c> sets it from <see cref="GoogleApiKeyVariable"/>,
/// so it can come from the environment and never from a file, and with none
/// Google is skipped.
/// </remarks>
public sealed class GeocodingOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "Geocoding";

    /// <summary>The environment variable Google's API key is read from.</summary>
    public const string GoogleApiKeyVariable = "Geocoding__GoogleApiKey";

    /// <summary>Whether an address is geocoded at all; off by default.</summary>
    public bool Enabled { get; set; }

    /// <summary>The Census Geocoder's one-line address lookup.</summary>
    public string CensusEndpoint { get; set; } =
        "https://geocoding.geo.census.gov/geocoder/locations/onelineaddress";

    /// <summary>How long the Census Geocoder may take, in seconds.</summary>
    public int CensusTimeoutSeconds { get; set; } = 10;

    /// <summary>Google's Geocoding API.</summary>
    public string GoogleEndpoint { get; set; } = "https://maps.googleapis.com/maps/api/geocode/json";

    /// <summary>How long Google may take, in seconds.</summary>
    public int GoogleTimeoutSeconds { get; set; } = 10;

    /// <summary>Google's API key, from the environment only; empty skips Google.</summary>
    public string? GoogleApiKey { get; set; }
}

/// <summary>Where an address is, and which service said so.</summary>
/// <param name="Latitude">The latitude, in decimal degrees.</param>
/// <param name="Longitude">The longitude, in decimal degrees.</param>
/// <param name="Source"><see cref="PersonAddress.Census"/> or <see cref="PersonAddress.Google"/>.</param>
public sealed record GeocodedPoint(decimal Latitude, decimal Longitude, string Source);

/// <summary>Finds the coordinates of an address.</summary>
public interface IGeocoder
{
    /// <summary>
    /// The coordinates of <paramref name="address"/>, or null where geocoding
    /// is off or no service found it.
    /// </summary>
    Task<GeocodedPoint?> Geocode(PersonAddress address, CancellationToken cancellationToken);
}

/// <summary>
/// The US Census Geocoder first, and Google's Geocoding API when Census fails,
/// times out or finds no match.
/// </summary>
/// <remarks>
/// Each service is sent the one-line street address — street, city, state and
/// zip — and nothing else about the person. A failure of a service is logged
/// and is never an exception: a save never waits on geocoding succeeding. The
/// one thing let through is the caller's own cancellation.
/// </remarks>
public sealed class Geocoder(
    HttpClient client,
    IOptions<GeocodingOptions> options,
    ILogger<Geocoder> logger) : IGeocoder
{
    /// <summary>
    /// Fills <paramref name="address"/>'s coordinates by <paramref name="geocoder"/>
    /// where it has a street and none, and leaves it as it is otherwise.
    /// </summary>
    public static async Task Fill(IGeocoder? geocoder, PersonAddress address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (geocoder is null || string.IsNullOrWhiteSpace(address.Street)
            || address.Latitude is not null || address.Longitude is not null)
        {
            return;
        }

        if (await geocoder.Geocode(address, cancellationToken) is { } point)
        {
            address.Latitude = point.Latitude;
            address.Longitude = point.Longitude;
            address.GeocodedBy = point.Source;
        }
    }

    /// <inheritdoc />
    public async Task<GeocodedPoint?> Geocode(PersonAddress address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);

        var settings = options.Value;
        if (!settings.Enabled || string.IsNullOrWhiteSpace(address.Street))
        {
            return null;
        }

        var oneLine = string.Join(
            ", ",
            new[] { address.Street, address.City, address.State, address.PostalCode }
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => part!.Trim()));

        var census = await Ask(
            "Census",
            $"{settings.CensusEndpoint}?address={Uri.EscapeDataString(oneLine)}&benchmark=Public_AR_Current&format=json",
            settings.CensusTimeoutSeconds,
            Census,
            cancellationToken);
        if (census is not null)
        {
            return census;
        }

        if (string.IsNullOrWhiteSpace(settings.GoogleApiKey))
        {
            logger.LogInformation("Not asking Google to geocode an address: no API key is set.");
            return null;
        }

        return await Ask(
            "Google",
            $"{settings.GoogleEndpoint}?address={Uri.EscapeDataString(oneLine)}&key={Uri.EscapeDataString(settings.GoogleApiKey)}",
            settings.GoogleTimeoutSeconds,
            Google,
            cancellationToken);
    }

    /// <summary>
    /// One service's answer read by <paramref name="read"/>, or null where it
    /// failed, timed out or found no match.
    /// </summary>
    private async Task<GeocodedPoint?> Ask(
        string service,
        string url,
        int timeoutSeconds,
        Func<JsonElement, GeocodedPoint?> read,
        CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try
        {
            using var response = await client.GetAsync(url, linked.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("{Service} did not geocode an address: {Status}.", service, (int)response.StatusCode);
                return null;
            }

            await using var body = await response.Content.ReadAsStreamAsync(linked.Token);
            using var json = await JsonDocument.ParseAsync(body, cancellationToken: linked.Token);
            var point = read(json.RootElement);
            if (point is null)
            {
                logger.LogInformation("{Service} found no match for an address.", service);
            }

            return point;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("{Service} did not geocode an address within {Seconds} seconds.", service, timeoutSeconds);
            return null;
        }
        catch (Exception failure) when (failure is HttpRequestException or JsonException
            or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            logger.LogWarning(failure, "{Service} could not geocode an address.", service);
            return null;
        }
    }

    /// <summary>The first of the Census Geocoder's address matches, whose x is the longitude.</summary>
    private static GeocodedPoint? Census(JsonElement root) =>
        root.TryGetProperty("result", out var result)
        && result.TryGetProperty("addressMatches", out var matches)
        && matches.ValueKind == JsonValueKind.Array
        && matches.GetArrayLength() > 0
        && matches[0].TryGetProperty("coordinates", out var coordinates)
            ? Point(coordinates.GetProperty("y"), coordinates.GetProperty("x"), PersonAddress.Census)
            : null;

    /// <summary>The first of Google's results, where its status is OK.</summary>
    private static GeocodedPoint? Google(JsonElement root) =>
        root.TryGetProperty("status", out var status) && status.GetString() == "OK"
        && root.TryGetProperty("results", out var results)
        && results.ValueKind == JsonValueKind.Array
        && results.GetArrayLength() > 0
            ? Point(
                results[0].GetProperty("geometry").GetProperty("location").GetProperty("lat"),
                results[0].GetProperty("geometry").GetProperty("location").GetProperty("lng"),
                PersonAddress.Google)
            : null;

    /// <summary>A pair rounded to the six places the columns hold.</summary>
    private static GeocodedPoint Point(JsonElement latitude, JsonElement longitude, string source) =>
        new(
            Math.Round(decimal.Parse(latitude.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture), 6),
            Math.Round(decimal.Parse(longitude.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture), 6),
            source);
}
