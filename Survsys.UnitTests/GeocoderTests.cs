using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Survsys.Api;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The address geocoding against a stub handler standing in for both
/// services: nothing here reaches the Census Geocoder or Google.
/// </summary>
public class GeocoderTests
{
    private const string CensusEndpoint = "http://census.test/onelineaddress";
    private const string GoogleEndpoint = "http://google.test/geocode";

    private const string CensusFound =
        """{"result":{"addressMatches":[{"coordinates":{"x":-72.6851,"y":41.7637}}]}}""";
    private const string CensusNoMatch = """{"result":{"addressMatches":[]}}""";
    private const string GoogleFound =
        """{"status":"OK","results":[{"geometry":{"location":{"lat":41.76371,"lng":-72.68509}}}]}""";
    private const string GoogleNoMatch = """{"status":"ZERO_RESULTS","results":[]}""";

    private static PersonAddress Address() =>
        new() { Street = "1 Main Street", City = "Hartford", State = "CT", PostalCode = "06103" };

    private static (Geocoder Geocoder, StubHandler Handler) Make(
        Func<HttpRequestMessage, HttpResponseMessage> answer,
        bool enabled = true,
        string? key = "google-key")
    {
        var handler = new StubHandler(answer);
        var geocoder = new Geocoder(
            new HttpClient(handler),
            Options.Create(new GeocodingOptions
            {
                Enabled = enabled,
                CensusEndpoint = CensusEndpoint,
                GoogleEndpoint = GoogleEndpoint,
                GoogleApiKey = key,
            }),
            NullLogger<Geocoder>.Instance);
        return (geocoder, handler);
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static bool IsCensus(HttpRequestMessage request) =>
        request.RequestUri!.Host == "census.test";

    [Fact]
    public void TheSectionBindsWithGeocodingOffByDefault()
    {
        var empty = new GeocodingOptions();
        new ConfigurationBuilder().Build().GetSection(GeocodingOptions.SectionName).Bind(empty);

        Assert.False(empty.Enabled);
        Assert.StartsWith("https://geocoding.geo.census.gov/", empty.CensusEndpoint, StringComparison.Ordinal);
        Assert.StartsWith("https://maps.googleapis.com/", empty.GoogleEndpoint, StringComparison.Ordinal);
        Assert.Null(empty.GoogleApiKey);

        var bound = new GeocodingOptions();
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Geocoding:Enabled"] = "true",
                ["Geocoding:CensusEndpoint"] = CensusEndpoint,
                ["Geocoding:CensusTimeoutSeconds"] = "3",
                ["Geocoding:GoogleTimeoutSeconds"] = "4",
            })
            .Build()
            .GetSection(GeocodingOptions.SectionName)
            .Bind(bound);

        Assert.True(bound.Enabled);
        Assert.Equal(CensusEndpoint, bound.CensusEndpoint);
        Assert.Equal(3, bound.CensusTimeoutSeconds);
        Assert.Equal(4, bound.GoogleTimeoutSeconds);
    }

    [Fact]
    public async Task GeocodeAsksNothingWhileOff()
    {
        var (geocoder, handler) = Make(_ => Json(CensusFound), enabled: false);

        Assert.Null(await geocoder.Geocode(Address(), CancellationToken.None));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GeocodeTakesTheCensusMatchAndSendsOnlyTheStreetAddress()
    {
        var (geocoder, handler) = Make(_ => Json(CensusFound));

        var point = await geocoder.Geocode(Address(), CancellationToken.None);

        Assert.Equal(new GeocodedPoint(41.7637m, -72.6851m, PersonAddress.Census), point);
        var asked = Assert.Single(handler.Requests);
        Assert.Contains(Uri.EscapeDataString("1 Main Street, Hartford, CT, 06103"), asked, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GeocodeFallsOverToGoogleWhenCensusFindsNothingOrFails(HttpStatusCode censusStatus)
    {
        var (geocoder, handler) = Make(request => IsCensus(request)
            ? censusStatus == HttpStatusCode.OK ? Json(CensusNoMatch) : new HttpResponseMessage(censusStatus)
            : Json(GoogleFound));

        var point = await geocoder.Geocode(Address(), CancellationToken.None);

        Assert.Equal(new GeocodedPoint(41.76371m, -72.68509m, PersonAddress.Google), point);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("key=google-key", handler.Requests[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task GeocodeFallsOverToGoogleWhenCensusCannotBeReached()
    {
        var (geocoder, _) = Make(request => IsCensus(request)
            ? throw new HttpRequestException("unreachable")
            : Json(GoogleFound));

        Assert.Equal(PersonAddress.Google, (await geocoder.Geocode(Address(), CancellationToken.None))?.Source);
    }

    [Fact]
    public async Task GeocodeReturnsNothingWhenBothFail()
    {
        var (geocoder, handler) = Make(request => IsCensus(request)
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : Json(GoogleNoMatch));

        Assert.Null(await geocoder.Geocode(Address(), CancellationToken.None));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GeocodeSkipsGoogleWithNoKey(string? key)
    {
        var (geocoder, handler) = Make(request => IsCensus(request) ? Json(CensusNoMatch) : Json(GoogleFound), key: key);

        Assert.Null(await geocoder.Geocode(Address(), CancellationToken.None));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GeocodeTreatsATimeoutBelowOneSecondAsOne()
    {
        var geocoder = new Geocoder(
            new HttpClient(new StubHandler(_ => Json(CensusFound))),
            Options.Create(new GeocodingOptions
            {
                Enabled = true,
                CensusEndpoint = CensusEndpoint,
                CensusTimeoutSeconds = -2,
                GoogleTimeoutSeconds = 0,
            }),
            NullLogger<Geocoder>.Instance);

        Assert.Equal(PersonAddress.Census, (await geocoder.Geocode(Address(), CancellationToken.None))?.Source);
    }

    [Fact]
    public async Task FillGeocodesAnAddressWithAStreetAndNoCoordinates()
    {
        var (geocoder, _) = Make(_ => Json(CensusFound));
        var address = Address();

        await Geocoder.Fill(geocoder, address, CancellationToken.None);

        Assert.Equal(41.7637m, address.Latitude);
        Assert.Equal(-72.6851m, address.Longitude);
        Assert.Equal(PersonAddress.Census, address.GeocodedBy);
    }

    [Fact]
    public async Task FillLeavesAnAddressWithCoordinatesOrNoStreetAlone()
    {
        var (geocoder, handler) = Make(_ => Json(CensusFound));
        var typed = Address();
        typed.Latitude = 1m;
        typed.Longitude = 2m;
        typed.GeocodedBy = PersonAddress.Manual;
        var noStreet = new PersonAddress { City = "Hartford", State = "CT" };

        await Geocoder.Fill(geocoder, typed, CancellationToken.None);
        await Geocoder.Fill(geocoder, noStreet, CancellationToken.None);
        await Geocoder.Fill(null, Address(), CancellationToken.None);

        Assert.Equal((1m, 2m, PersonAddress.Manual), (typed.Latitude, typed.Longitude, typed.GeocodedBy));
        Assert.Null(noStreet.Latitude);
        Assert.Empty(handler.Requests);
    }

    /// <summary>Answers every request with the test's function, keeping each URL asked.</summary>
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.AbsoluteUri);
            return Task.FromResult(answer(request));
        }
    }
}
