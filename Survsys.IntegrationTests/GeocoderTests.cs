using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Survsys.Api;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// <see cref="Geocoder"/> against a loopback host standing in for both the
/// Census Geocoder and Google, so the lookups are proved over HTTP — the
/// request, the answer read and the failover — without reaching either
/// service.
/// </summary>
public sealed class GeocoderTests
{
    private static PersonAddress Address(string street = "1 Main Street") =>
        new() { Street = street, City = "Hartford", State = "CT", PostalCode = "06103" };

    private static Geocoder Geocoder(GeocodingStub stub, string? key = "stub-key", int censusTimeoutSeconds = 10) =>
        new(
            new HttpClient(),
            Options.Create(stub.Options(key, censusTimeoutSeconds)),
            NullLogger<Geocoder>.Instance);

    [Fact]
    public async Task CensusFoundIsTakenAndGoogleIsNotAsked()
    {
        await using var stub = await GeocodingStub.Start();

        var point = await Geocoder(stub).Geocode(Address(), CancellationToken.None);

        Assert.Equal(new GeocodedPoint(41.7637m, -72.6851m, PersonAddress.Census), point);
        var asked = Assert.Single(stub.CensusAsked);
        Assert.Equal("1 Main Street, Hartford, CT, 06103", asked);
        Assert.Empty(stub.GoogleAsked);
    }

    [Theory]
    [InlineData(GeocodingStub.Answer.Fail)]
    [InlineData(GeocodingStub.Answer.NoMatch)]
    [InlineData(GeocodingStub.Answer.Slow)]
    public async Task CensusFailingFallsOverToGoogle(GeocodingStub.Answer census)
    {
        await using var stub = await GeocodingStub.Start();
        stub.Census = census;

        var point = await Geocoder(stub, censusTimeoutSeconds: 1).Geocode(Address(), CancellationToken.None);

        Assert.Equal(new GeocodedPoint(41.76371m, -72.68509m, PersonAddress.Google), point);
        Assert.Equal("1 Main Street, Hartford, CT, 06103", Assert.Single(stub.GoogleAsked));
    }

    [Fact]
    public async Task BothFailingFindsNothing()
    {
        await using var stub = await GeocodingStub.Start();
        stub.Census = GeocodingStub.Answer.Fail;
        stub.Google = GeocodingStub.Answer.NoMatch;

        Assert.Null(await Geocoder(stub).Geocode(Address(), CancellationToken.None));
        Assert.Single(stub.CensusAsked);
        Assert.Single(stub.GoogleAsked);
    }

    [Fact]
    public async Task NoGoogleKeySkipsGoogle()
    {
        await using var stub = await GeocodingStub.Start();
        stub.Census = GeocodingStub.Answer.NoMatch;

        Assert.Null(await Geocoder(stub, key: null).Geocode(Address(), CancellationToken.None));
        Assert.Single(stub.CensusAsked);
        Assert.Empty(stub.GoogleAsked);
    }
}

/// <summary>
/// A loopback host answering as the Census Geocoder at <c>/census</c> and as
/// Google at <c>/google</c>, keeping the one-line address each was asked. A
/// found address is placed by its street: "1 Main Street" where Census found
/// it, "9 Elm Court" further north, anything else at the same point.
/// </summary>
public sealed class GeocodingStub : IAsyncDisposable
{
    private readonly WebApplication app;

    private GeocodingStub(WebApplication app) => this.app = app;

    /// <summary>How one service answers.</summary>
    public enum Answer
    {
        Found,
        NoMatch,
        Fail,
        Slow,
    }

    public Answer Census { get; set; } = Answer.Found;

    public Answer Google { get; set; } = Answer.Found;

    public List<string> CensusAsked { get; } = [];

    public List<string> GoogleAsked { get; } = [];

    public string Url => app.Urls.First();

    public static async Task<GeocodingStub> Start()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();

        var app = builder.Build();
        app.Urls.Clear();
        app.Urls.Add("http://127.0.0.1:0");

        var stub = new GeocodingStub(app);
        app.Run(stub.Respond);
        await app.StartAsync();

        return stub;
    }

    /// <summary>Settings pointing both services here, geocoding on.</summary>
    public GeocodingOptions Options(string? key = "stub-key", int censusTimeoutSeconds = 10) =>
        new()
        {
            Enabled = true,
            CensusEndpoint = $"{Url}/census",
            CensusTimeoutSeconds = censusTimeoutSeconds,
            GoogleEndpoint = $"{Url}/google",
            GoogleApiKey = key,
        };

    public async ValueTask DisposeAsync() => await app.DisposeAsync();

    private async Task Respond(HttpContext context)
    {
        var address = context.Request.Query["address"].ToString();
        var census = context.Request.Path == "/census";
        lock (this)
        {
            (census ? CensusAsked : GoogleAsked).Add(address);
        }

        var answer = census ? Census : Google;
        if (answer == Answer.Slow)
        {
            await Task.Delay(TimeSpan.FromSeconds(3), context.RequestAborted);
        }

        if (answer == Answer.Fail)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        var north = address.StartsWith("9 Elm Court", StringComparison.Ordinal) ? 0.01m : 0m;
        var censusY = (41.7637m + north).ToString(CultureInfo.InvariantCulture);
        var googleLat = (41.76371m + north).ToString(CultureInfo.InvariantCulture);
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            (census, answer) switch
            {
                (true, Answer.NoMatch) => """{"result":{"addressMatches":[]}}""",
                (true, _) => $$$"""{"result":{"addressMatches":[{"coordinates":{"x":-72.6851,"y":{{{censusY}}}}}]}}""",
                (false, Answer.NoMatch) => """{"status":"ZERO_RESULTS","results":[]}""",
                (false, _) => $$$$"""{"status":"OK","results":[{"geometry":{"location":{"lat":{{{{googleLat}}}},"lng":-72.68509}}}]}""",
            });
    }
}
