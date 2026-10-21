using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Survsys.Api;
using Survsys.Contracts;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// Sending a case to IBIS, against the real database and a loopback stub
/// standing in for IBIS: that an accepted transmission marks the fixture's
/// seeded report sent today and carried its record number, that a rejected one
/// is a 502 that leaves the report unmarked, and that an unknown case is a 404.
/// </summary>
/// <remarks>
/// The endpoint the API tier transmits to arrives as an environment variable,
/// for the reason <see cref="SurvsysApi"/> gives for its connection string, and
/// so each test starts its own host after pointing it at its own stub. The
/// seeded report's IBIS state is reset after each test, since the suite shares
/// the one database.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class IbisEndpointsTests(SqlServerFixture server)
{
    private const string EndpointVariable = "IbisTransmission__Endpoint";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task AnAcceptedTransmissionMarksTheCaseSentToday()
    {
        IbisCaseRecord? received = null;

        var (stub, url) = await StartStub(async context =>
        {
            received = await context.Request.ReadFromJsonAsync<IbisCaseRecord>();
            context.Response.StatusCode = StatusCodes.Status200OK;
        });

        try
        {
            using var response = await Send(url, await SeededReportId());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var today = DateOnly.FromDateTime(DateTimeOffset.Now.Date);
            var body = (await response.Content.ReadFromJsonAsync<IbisTransmissionResponse>(Json))!;
            Assert.Equal("Accepted", body.Status);
            Assert.True(body.SentToIbis);
            Assert.Equal(today, body.IbisUpdatedAt);

            Assert.Equal(SqlServerFixture.SeededReportRecordNumber, received?.RecordNumber);
            Assert.Equal(SqlServerFixture.SeededReportCondition, received?.Condition);

            var (sent, updatedAt) = await IbisState();
            Assert.True(sent);
            Assert.Equal(today, updatedAt);
        }
        finally
        {
            await stub.DisposeAsync();
            await Reset();
        }
    }

    [Fact]
    public async Task ARejectedTransmissionIsA502AndLeavesTheCaseUnmarked()
    {
        var (stub, url) = await StartStub(context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Task.CompletedTask;
        });

        try
        {
            using var response = await Send(url, await SeededReportId());

            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

            var (sent, updatedAt) = await IbisState();
            Assert.Null(sent);
            Assert.Null(updatedAt);
        }
        finally
        {
            await stub.DisposeAsync();
            await Reset();
        }
    }

    [Fact]
    public async Task AnUnknownCaseIsA404()
    {
        using var response = await Send("http://127.0.0.1:9/", int.MaxValue);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Starts the API tier pointed at <paramref name="endpoint"/> and sends the
    /// case to IBIS through it.
    /// </summary>
    private async Task<HttpResponseMessage> Send(string endpoint, int id)
    {
        Environment.SetEnvironmentVariable(EndpointVariable, endpoint);

        try
        {
            using var api = new SurvsysApi(server.ConnectionString);
            return await api.CreateClient().PostAsync($"/api/person-conditions/{id}/ibis", null);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EndpointVariable, null);
        }
    }

    /// <summary>
    /// A loopback host that answers every request with <paramref name="handler"/>,
    /// bound to an OS-assigned port, standing in for IBIS.
    /// </summary>
    private static async Task<(WebApplication App, string Url)> StartStub(RequestDelegate handler)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();

        var app = builder.Build();
        app.Urls.Clear();
        app.Urls.Add("http://127.0.0.1:0");
        app.Run(handler);
        await app.StartAsync();

        return (app, app.Urls.First());
    }

    private async Task<int> SeededReportId()
    {
        using var context = server.NewContext();
        return await context.PersonConditions
            .Where(report => report.RecordNumber == SqlServerFixture.SeededReportRecordNumber)
            .Select(report => report.Id)
            .SingleAsync();
    }

    private async Task<(bool? Sent, DateOnly? UpdatedAt)> IbisState()
    {
        using var context = server.NewContext();
        var report = await context.PersonConditions
            .AsNoTracking()
            .SingleAsync(report => report.RecordNumber == SqlServerFixture.SeededReportRecordNumber);
        return (report.SentToIbis, report.IbisUpdatedAt);
    }

    private async Task Reset()
    {
        using var context = server.NewContext();
        await context.PersonConditions
            .Where(report => report.RecordNumber == SqlServerFixture.SeededReportRecordNumber)
            .ExecuteUpdateAsync(set => set
                .SetProperty(report => report.SentToIbis, (bool?)null)
                .SetProperty(report => report.IbisUpdatedAt, (DateOnly?)null));
    }
}
