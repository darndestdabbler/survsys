using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// That the classification a save answers with in its header is recorded in
/// the circuit's <see cref="CaseClassifications"/>, and that a response
/// without one, or outside a circuit, passes untouched.
/// </summary>
public class CaseClassificationHandlerTests
{
    private const string Header =
        """{"reportId":7,"caseStatus":"C","findings":["Chlamydia: confirmed."]}""";

    [Fact]
    public async Task SendAsyncRecordsTheHeadersClassificationInTheCircuit()
    {
        var classifications = new CaseClassifications();
        var circuit = Circuit(classifications);

        try
        {
            using var response = await Send(circuit, Header);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var last = classifications.Last!;
            Assert.Equal(7, last.ReportId);
            Assert.Equal("C", last.CaseStatus);
            Assert.Equal(["Chlamydia: confirmed."], last.Findings);
        }
        finally
        {
            circuit.Services = null;
        }
    }

    [Fact]
    public async Task SendAsyncRecordsNothingForAResponseWithoutTheHeader()
    {
        var classifications = new CaseClassifications();
        var circuit = Circuit(classifications);

        try
        {
            using var _ = await Send(circuit, null);

            Assert.Null(classifications.Last);
        }
        finally
        {
            circuit.Services = null;
        }
    }

    [Fact]
    public async Task SendAsyncPassesTheResponseOutsideACircuit()
    {
        using var response = await Send(new CircuitServicesAccessor(), Header);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static CircuitServicesAccessor Circuit(CaseClassifications classifications) =>
        new()
        {
            Services = new ServiceCollection().AddSingleton(classifications).BuildServiceProvider(),
        };

    private static async Task<HttpResponseMessage> Send(CircuitServicesAccessor circuit, string? header)
    {
        using var handler = new CaseClassificationHandler(circuit)
        {
            InnerHandler = new StubTransport(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                if (header is not null)
                {
                    response.Headers.Add(CaseClassificationHandler.HeaderName, header);
                }

                return response;
            }),
        };
        using var http = new HttpClient(handler, disposeHandler: false);

        return await http.PutAsync("http://api.example.test/api/person-conditions/7/clinical", null);
    }

    private sealed class StubTransport(Func<HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(answer());
    }
}
