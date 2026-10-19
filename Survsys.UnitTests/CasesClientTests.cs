using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader of <c>/api/cases</c>: the URL a search asks
/// for, and what comes back.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s, which proves the
/// handling these clients share. What is proved here is what is this client's
/// own, and most of it is the query string: a criterion nobody typed must be
/// absent rather than empty, a value must arrive as the value it was, and an
/// id or a date must be written as the other tier parses it.
/// </remarks>
public class CasesClientTests
{
    private const string Json = "application/json";

    private const string EmptyPage =
        """{ "items": [], "page": 1, "pageSize": 50, "totalCount": 0 }""";

    [Fact]
    public void TheUrlCarriesThePageAndNoCriterionNobodyTyped()
    {
        Assert.Equal(
            "api/cases?page=1&pageSize=50",
            CasesClient.Url(new CaseSearchCriteria(), 1, 50));
    }

    [Fact]
    public void TheUrlCarriesEveryCriterionThatWasTyped()
    {
        var url = CasesClient.Url(
            new CaseSearchCriteria(
                RecordNumber: "2026",
                LastName: "Rodriguez",
                FirstName: "Maria",
                ConditionId: 74,
                AgencyId: 5,
                PersonId: 13,
                WorkflowState: "accepted",
                CaseStatus: "C",
                OnsetFrom: new DateOnly(2026, 2, 1),
                OnsetTo: new DateOnly(2026, 2, 28)),
            2,
            25);

        Assert.Equal(
            "api/cases?page=2&pageSize=25&recordNumber=2026&lastName=Rodriguez&firstName=Maria"
            + "&conditionId=74&agencyId=5&personId=13&workflowState=accepted&caseStatus=C"
            + "&onsetFrom=2026-02-01&onsetTo=2026-02-28",
            url);
    }

    /// <summary>
    /// A box nobody typed in is left out of the request: a blank text
    /// criterion would otherwise search for a prefix of nothing, and an
    /// unchosen id or date left in as <c>?conditionId=</c> would be a request
    /// the API tier refuses rather than a criterion it ignores.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void ABlankCriterionIsLeftOutRatherThanSentEmpty(string blank)
    {
        var url = CasesClient.Url(
            new CaseSearchCriteria(
                RecordNumber: blank,
                LastName: blank,
                FirstName: blank,
                ConditionId: null,
                AgencyId: null,
                PersonId: null,
                WorkflowState: blank,
                CaseStatus: blank,
                OnsetFrom: null,
                OnsetTo: null),
            1,
            50);

        Assert.Equal("api/cases?page=1&pageSize=50", url);
    }

    [Fact]
    public void ACriterionIsTrimmedBeforeItIsSent()
    {
        var url = CasesClient.Url(
            new CaseSearchCriteria(RecordNumber: " 2026 ", LastName: "  Rodriguez ", FirstName: "M"),
            1,
            50);

        Assert.Contains("recordNumber=2026&lastName=Rodriguez&firstName=M", url, StringComparison.Ordinal);
    }

    /// <summary>
    /// A name is a name and not query-string syntax: an ampersand would
    /// otherwise start a parameter of its own, a plus would arrive as a space,
    /// and a hash would truncate the request.
    /// </summary>
    [Theory]
    [InlineData("O'Connor & Sons", "O%27Connor%20%26%20Sons")]
    [InlineData("a+b", "a%2Bb")]
    [InlineData("Smith#1", "Smith%231")]
    [InlineData("Ní Bhriain", "N%C3%AD%20Bhriain")]
    public void ACriterionIsEscaped(string typed, string expected)
    {
        Assert.Contains(
            $"lastName={expected}",
            CasesClient.Url(new CaseSearchCriteria(LastName: typed), 1, 50),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The dates are written the way the other tier parses them, and not the
    /// way the browser's culture would write them: a date formatted
    /// <c>01/02/2026</c> is the first of February in half the world and the
    /// second of January in the other half.
    /// </summary>
    [Fact]
    public void TheOnsetDatesAreWrittenAsTheApiTierParsesThem()
    {
        var url = CasesClient.Url(
            new CaseSearchCriteria(OnsetFrom: new DateOnly(2026, 1, 2), OnsetTo: new DateOnly(2026, 12, 31)),
            1,
            50);

        Assert.Contains("onsetFrom=2026-01-02", url, StringComparison.Ordinal);
        Assert.Contains("onsetTo=2026-12-31", url, StringComparison.Ordinal);
    }

    /// <summary>
    /// Either end of the range may be given alone, and the one not given is
    /// left out rather than sent as a date of nothing.
    /// </summary>
    [Fact]
    public void OneEndOfTheOnsetRangeAloneIsSentAlone()
    {
        var from = CasesClient.Url(new CaseSearchCriteria(OnsetFrom: new DateOnly(2026, 3, 1)), 1, 50);
        var to = CasesClient.Url(new CaseSearchCriteria(OnsetTo: new DateOnly(2026, 3, 31)), 1, 50);

        Assert.Equal("api/cases?page=1&pageSize=50&onsetFrom=2026-03-01", from);
        Assert.Equal("api/cases?page=1&pageSize=50&onsetTo=2026-03-31", to);
    }

    [Fact]
    public async Task TheRequestAsksForTheUrlTheCriteriaBuild()
    {
        Uri? asked = null;

        var client = new CasesClient(Transport(request =>
        {
            asked = request.RequestUri;
            return Answer(HttpStatusCode.OK, EmptyPage);
        }));

        await client.Page(new CaseSearchCriteria(LastName: "Rod", PersonId: 13), 1, 50);

        Assert.Equal(
            "http://api.example.test/api/cases?page=1&pageSize=50&lastName=Rod&personId=13",
            asked?.ToString());
    }

    [Fact]
    public async Task APageTheApiReturnedIsThePageTheClientReturns()
    {
        var client = new CasesClient(Transport(_ => Answer(
            HttpStatusCode.OK,
            """
            {
              "items": [
                {
                  "id": 11, "recordNumber": "202600004", "personId": 13,
                  "patientName": "Rodriguez, Maria", "conditionId": 74,
                  "conditionName": "Malaria", "agencyId": 5,
                  "agencyName": "Salt Lake County", "workflowState": "accepted",
                  "caseStatus": "C", "mmwrWeek": 11, "mmwrYear": 2026,
                  "eventOnsetDate": "2026-03-08"
                }
              ],
              "page": 1,
              "pageSize": 50,
              "totalCount": 1
            }
            """)));

        var asked = await client.Page(new CaseSearchCriteria(LastName: "Rod"), 1, 50);

        Assert.True(asked.Succeeded);
        Assert.Equal(1, asked.Value!.TotalCount);

        var row = Assert.Single(asked.Value.Items);
        Assert.Equal(
            new(11, "202600004", 13, "Rodriguez, Maria", 74, "Malaria", 5, "Salt Lake County",
                "accepted", "C", 11, 2026, new DateOnly(2026, 3, 8)),
            row);
    }

    /// <summary>
    /// A row for a case with no agency, no status, no week and no onset reads
    /// back with those null, as the API tier sent them.
    /// </summary>
    [Fact]
    public async Task ARowWithNothingOptionalSetReadsBackWithNulls()
    {
        var client = new CasesClient(Transport(_ => Answer(
            HttpStatusCode.OK,
            """
            {
              "items": [
                {
                  "id": 12, "recordNumber": "202600005", "personId": 14,
                  "patientName": "Doe", "conditionId": 74, "conditionName": "Malaria",
                  "agencyId": null, "agencyName": null, "workflowState": "assigned_to_lhd",
                  "caseStatus": null, "mmwrWeek": null, "mmwrYear": null, "eventOnsetDate": null
                }
              ],
              "page": 1, "pageSize": 50, "totalCount": 1
            }
            """)));

        var asked = await client.Page(new CaseSearchCriteria(), 1, 50);

        var row = Assert.Single(asked.Value!.Items);
        Assert.Null(row.AgencyId);
        Assert.Null(row.AgencyName);
        Assert.Null(row.CaseStatus);
        Assert.Null(row.MmwrWeek);
        Assert.Null(row.MmwrYear);
        Assert.Null(row.EventOnsetDate);
    }

    /// <summary>
    /// A search that failed says so about the cases, and names the status and
    /// not the API tier's own account of what was wrong.
    /// </summary>
    [Fact]
    public async Task ASearchThatFailedBecomesAMessageAboutTheCases()
    {
        var client = new CasesClient(Transport(_ => Answer(
            HttpStatusCode.BadRequest,
            """{ "status": 400, "detail": "pageSize must be 200 or fewer, and so on." }""")));

        var asked = await client.Page(new CaseSearchCriteria(), 1, 500);

        Assert.False(asked.Succeeded);
        Assert.Null(asked.Value);
        Assert.StartsWith(CasesClient.SearchFailurePrefix, asked.Failure);
        Assert.Contains("400", asked.Failure);
        Assert.DoesNotContain("pageSize", asked.Failure, StringComparison.Ordinal);
    }

    /// <summary>
    /// A screen reads this to tell "nothing typed" apart from "nothing
    /// found".
    /// </summary>
    [Fact]
    public void CriteriaNobodyTypedIntoAreEmpty()
    {
        Assert.True(new CaseSearchCriteria().IsEmpty);
        Assert.True(new CaseSearchCriteria(LastName: "  ", RecordNumber: "").IsEmpty);
        Assert.False(new CaseSearchCriteria(LastName: "Rod").IsEmpty);
        Assert.False(new CaseSearchCriteria(ConditionId: 74).IsEmpty);
        Assert.False(new CaseSearchCriteria(PersonId: 13).IsEmpty);
        Assert.False(new CaseSearchCriteria(OnsetTo: new DateOnly(2026, 3, 31)).IsEmpty);
    }

    private static HttpClient Transport(Func<HttpRequestMessage, HttpResponseMessage> answer) =>
        new(new StubTransport(answer)) { BaseAddress = new Uri("http://api.example.test/") };

    private static HttpResponseMessage Answer(
        HttpStatusCode status,
        string body,
        string mediaType = Json) =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8)
            {
                Headers = { ContentType = new MediaTypeHeaderValue(mediaType) },
            },
        };

    private sealed class StubTransport(Func<HttpRequestMessage, HttpResponseMessage> answer)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(answer(request));
    }
}
