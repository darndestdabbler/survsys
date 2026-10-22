using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of <c>/api/system-settings</c>:
/// the request each call sends, what it hands the screen, and the phrase its
/// failures start with. Every way a JSON ask can fail is
/// <see cref="ApiAskTests"/>'s.
/// </summary>
public class SystemSettingsClientTests
{
    private const string Prefix = """{ "name": "cdc_batch_file_prefix", "value": "CT", "description": "CDC batch file naming" }""";

    [Fact]
    public async Task AllAsksForTheSettings()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, $"[{Prefix}]");

        var asked = await client.All();

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/system-settings", sent?.RequestUri?.ToString());
        Assert.Equal(
            new SystemSettingResponse("cdc_batch_file_prefix", "CT", "CDC batch file naming"),
            Assert.Single(asked.Value!));
    }

    [Fact]
    public async Task SavePutsTheSettingUnderItsEscapedName()
    {
        HttpRequestMessage? sent = null;
        string? body = null;
        var client = Recording(request =>
        {
            sent = request;
            body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
        }, HttpStatusCode.OK, Prefix);

        var asked = await client.Save("cdc batch/prefix", new SaveSystemSettingRequest("CT", "CDC batch file naming"));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/system-settings/cdc%20batch%2Fprefix", sent?.RequestUri?.AbsoluteUri);
        Assert.Contains("\"value\":\"CT\"", body, StringComparison.Ordinal);
        Assert.Equal("CT", asked.Value?.Value);
    }

    [Fact]
    public async Task ASaveThatWasRefusedCarriesTheSavePrefix()
    {
        var client = Recording(_ => { }, HttpStatusCode.BadRequest, """{ "status": 400, "detail": "value is required." }""");

        var asked = await client.Save("cdc_batch_file_prefix", new SaveSystemSettingRequest("CT"));

        Assert.False(asked.Succeeded);
        Assert.StartsWith(SystemSettingsClient.SaveFailurePrefix, asked.Failure);
        Assert.Contains("400", asked.Failure);
    }

    [Fact]
    public async Task DeleteDeletesTheNamedSetting()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.NoContent, "");

        var failure = await client.Delete("cdc_batch_file_prefix");

        Assert.Null(failure);
        Assert.Equal(HttpMethod.Delete, sent?.Method);
        Assert.Equal("http://api.example.test/api/system-settings/cdc_batch_file_prefix", sent?.RequestUri?.ToString());
    }

    private static SystemSettingsClient Recording(
        Action<HttpRequestMessage> record,
        HttpStatusCode status,
        string body) =>
        new(new HttpClient(new StubTransport(request =>
        {
            record(request);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("application/json") },
                },
            };
        }))
        { BaseAddress = new Uri("http://api.example.test/") });

    private sealed class StubTransport(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(answer(request));
    }
}
