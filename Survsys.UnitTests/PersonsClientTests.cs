using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of <c>/api/persons</c>: the URL a
/// search asks for, the body a creation sends, and which of its two failure
/// phrases a message carries.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s, which proves the
/// handling these clients share. What is proved here is what is this client's
/// own, and most of it is the query string: a criterion nobody typed must be
/// absent rather than empty, and a value must arrive as the value it was.
/// </remarks>
public class PersonsClientTests
{
    private const string Json = "application/json";

    private const string EmptyPage =
        """{ "items": [], "page": 1, "pageSize": 50, "totalCount": 0 }""";

    [Fact]
    public void TheUrlCarriesThePageAndNoCriterionNobodyTyped()
    {
        Assert.Equal(
            "api/persons?page=1&pageSize=50",
            PersonsClient.Url(new PersonSearchCriteria(), 1, 50));
    }

    [Fact]
    public void TheUrlCarriesEveryCriterionThatWasTyped()
    {
        var url = PersonsClient.Url(
            new PersonSearchCriteria(
                LastName: "Rodriguez",
                FirstName: "Maria",
                MiddleName: "Elena",
                BirthDate: new DateOnly(1985, 6, 15),
                Phone: "8015551234",
                BirthGender: "F"),
            2,
            25);

        Assert.Equal(
            "api/persons?page=2&pageSize=25&lastName=Rodriguez&firstName=Maria"
            + "&middleName=Elena&phone=8015551234&birthGender=F&birthDate=1985-06-15",
            url);
    }

    /// <summary>
    /// A box nobody typed in is left out of the request, and that matters most
    /// for the date: the API tier reads <c>birthDate</c> as a date, so
    /// <c>?birthDate=</c> would be a request it refuses rather than a
    /// criterion it ignores.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void ABlankCriterionIsLeftOutRatherThanSentEmpty(string blank)
    {
        var url = PersonsClient.Url(
            new PersonSearchCriteria(
                LastName: blank,
                FirstName: blank,
                MiddleName: blank,
                BirthDate: null,
                Phone: blank,
                BirthGender: blank),
            1,
            50);

        Assert.Equal("api/persons?page=1&pageSize=50", url);
        Assert.DoesNotContain("birthDate", url, StringComparison.Ordinal);
    }

    [Fact]
    public void ACriterionIsTrimmedBeforeItIsSent()
    {
        Assert.Contains(
            "lastName=Rodriguez&",
            PersonsClient.Url(new PersonSearchCriteria(LastName: "  Rodriguez ", FirstName: "M"), 1, 50),
            StringComparison.Ordinal);
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
            PersonsClient.Url(new PersonSearchCriteria(LastName: typed), 1, 50),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The date is written the way the other tier parses it, and not the way
    /// the browser's culture would write it: a date formatted
    /// <c>15/06/1985</c> would arrive as a date the API refuses, and one
    /// formatted <c>06/15/1985</c> would arrive as a different day in half the
    /// world.
    /// </summary>
    [Fact]
    public void TheDateOfBirthIsWrittenAsTheApiTierParsesIt()
    {
        Assert.Contains(
            "birthDate=1985-06-15",
            PersonsClient.Url(new PersonSearchCriteria(BirthDate: new DateOnly(1985, 6, 15)), 1, 50),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheRequestAsksForTheUrlTheCriteriaBuild()
    {
        Uri? asked = null;

        var client = new PersonsClient(Transport(request =>
        {
            asked = request.RequestUri;
            return Answer(HttpStatusCode.OK, EmptyPage);
        }));

        await client.Page(new PersonSearchCriteria(LastName: "Rod"), 1, 50);

        Assert.Equal(
            "http://api.example.test/api/persons?page=1&pageSize=50&lastName=Rod",
            asked?.ToString());
    }

    [Fact]
    public async Task APageTheApiReturnedIsThePageTheClientReturns()
    {
        var client = new PersonsClient(Transport(_ => Answer(
            HttpStatusCode.OK,
            """
            {
              "items": [
                {
                  "id": 11, "lastName": "Rodriguez", "firstName": "Maria",
                  "middleName": "Elena", "birthDate": "1985-06-15",
                  "birthGender": "F", "currentGender": "F"
                }
              ],
              "page": 1,
              "pageSize": 50,
              "totalCount": 1
            }
            """)));

        var asked = await client.Page(new PersonSearchCriteria(LastName: "Rod"), 1, 50);

        Assert.True(asked.Succeeded);

        var person = Assert.Single(asked.Value!.Items);
        Assert.Equal(
            new(11, "Rodriguez", "Maria", "Elena", new DateOnly(1985, 6, 15), "F", "F"),
            person);
    }

    [Fact]
    public async Task APersonCreatedIsThePersonTheClientReturns()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new PersonsClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;

            return Answer(
                HttpStatusCode.Created,
                """
                {
                  "id": 42, "lastName": "Thorsen", "firstName": "Inge",
                  "middleName": null, "birthDate": null,
                  "birthGender": null, "currentGender": null
                }
                """);
        }));

        var created = await client.Create(new CreatePersonRequest("Thorsen", "Inge"));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/persons", sent?.RequestUri?.ToString());
        Assert.Contains("\"lastName\":\"Thorsen\"", body, StringComparison.Ordinal);

        Assert.True(created.Succeeded);
        Assert.Equal(42, created.Value?.Id);
    }

    [Fact]
    public async Task ThePersonFoundIsThePersonTheClientReturns()
    {
        Uri? asked = null;

        var client = new PersonsClient(Transport(request =>
        {
            asked = request.RequestUri;

            return Answer(
                HttpStatusCode.OK,
                """
                {
                  "id": 11, "lastName": "Rodriguez", "firstName": "Maria",
                  "middleName": "Elena", "birthDate": "1985-06-15",
                  "daysOldNoBirthday": null, "birthGender": "F",
                  "currentGender": "F", "ethnicity": "H", "language": "es",
                  "countryOfBirth": "MEX", "races": ["W"], "aliases": [],
                  "parentGuardians": [], "addresses": [], "telephones": [],
                  "emails": []
                }
                """);
        }));

        var found = await client.Find(11);

        Assert.Equal("http://api.example.test/api/persons/11", asked?.ToString());
        Assert.True(found.Succeeded);
        Assert.Equal("Rodriguez", found.Value?.LastName);
        Assert.Equal("Maria", found.Value?.FirstName);
        Assert.Equal(new DateOnly(1985, 6, 15), found.Value?.BirthDate);
    }

    /// <summary>
    /// An identifier nobody has: the API tier answers 404, and what the screen
    /// gets is a message rather than an exception or an empty person.
    /// </summary>
    [Fact]
    public async Task APersonNobodyHasBecomesAMessage()
    {
        var client = new PersonsClient(Transport(_ => Answer(
            HttpStatusCode.NotFound, """{ "status": 404 }""")));

        var found = await client.Find(987654);

        Assert.False(found.Succeeded);
        Assert.Null(found.Value);
        Assert.StartsWith(PersonsClient.FindFailurePrefix, found.Failure);
        Assert.Contains("404", found.Failure);
    }

    /// <summary>
    /// The save the Demographic tab makes: the record goes as the body of a
    /// PUT to the person's own URL, and what comes back is the detail the API
    /// tier now holds — with the identifiers it assigned, which the screen
    /// did not send.
    /// </summary>
    [Fact]
    public async Task TheRecordSavedIsSentWholeAndTheDetailComesBack()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new PersonsClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;

            return Answer(
                HttpStatusCode.OK,
                """
                {
                  "id": 11, "lastName": "Garcia", "firstName": "Mariela",
                  "middleName": null, "birthDate": "1986-07-16",
                  "daysOldNoBirthday": null, "birthGender": "F",
                  "currentGender": null, "ethnicity": null, "language": "en",
                  "countryOfBirth": null, "races": ["AA", "B"], "aliases": ["Mariela Garcia"],
                  "parentGuardians": ["Ana Garcia"],
                  "addresses": [
                    {
                      "id": 31, "street": "9 Elm Court", "unitNumber": null, "city": "Ogden",
                      "state": "UT", "postalCode": "84401", "county": "WE",
                      "beginning": "2020-01-02", "ending": null, "note": null,
                      "residenceTypeId": null
                    }
                  ],
                  "telephones": [
                    {
                      "id": 32, "telephoneType": null, "countryCode": null, "areaCode": "385",
                      "phoneNumber": "5550100", "extension": null, "beginning": null, "note": null
                    }
                  ],
                  "emails": [{ "id": 33, "emailAddress": "mariela@example.com", "currentEmail": true }]
                }
                """);
        }));

        var saved = await client.Update(11, new UpdatePersonRequest(
            LastName: "Garcia",
            FirstName: "Mariela",
            BirthDate: new DateOnly(1986, 7, 16),
            BirthGender: "F",
            Language: "en")
        {
            Races = ["B", "AA"],
            Aliases = ["Mariela Garcia"],
            ParentGuardians = ["Ana Garcia"],
            Addresses =
            [
                new PersonAddressRequest(
                    Street: "9 Elm Court", City: "Ogden", State: "UT", PostalCode: "84401",
                    County: "WE", Beginning: new DateOnly(2020, 1, 2)),
            ],
            Telephones = [new PersonTelephoneRequest(AreaCode: "385", PhoneNumber: "5550100")],
            Emails = [new PersonEmailRequest("mariela@example.com", true)],
        });

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/persons/11", sent?.RequestUri?.ToString());

        Assert.Contains("\"lastName\":\"Garcia\"", body, StringComparison.Ordinal);
        Assert.Contains("\"birthDate\":\"1986-07-16\"", body, StringComparison.Ordinal);
        Assert.Contains("\"races\":[\"B\",\"AA\"]", body, StringComparison.Ordinal);
        Assert.Contains("\"aliases\":[\"Mariela Garcia\"]", body, StringComparison.Ordinal);
        Assert.Contains("\"parentGuardians\":[\"Ana Garcia\"]", body, StringComparison.Ordinal);
        Assert.Contains("\"street\":\"9 Elm Court\"", body, StringComparison.Ordinal);
        Assert.Contains("\"beginning\":\"2020-01-02\"", body, StringComparison.Ordinal);
        Assert.Contains("\"phoneNumber\":\"5550100\"", body, StringComparison.Ordinal);
        Assert.Contains("\"emailAddress\":\"mariela@example.com\"", body, StringComparison.Ordinal);

        // A row's identifier is the API tier's to assign, so it is not in what
        // this one sends.
        Assert.DoesNotContain("\"id\"", body, StringComparison.Ordinal);

        Assert.True(saved.Succeeded);
        Assert.Equal(11, saved.Value?.Id);
        Assert.Equal("Garcia", saved.Value?.LastName);
        Assert.Equal(["AA", "B"], saved.Value?.Races);
        Assert.Equal(31, Assert.Single(saved.Value!.Addresses).Id);
        Assert.Equal(32, Assert.Single(saved.Value.Telephones).Id);
        Assert.Equal(33, Assert.Single(saved.Value.Emails).Id);
    }

    /// <summary>
    /// A save that failed says so about saving, and names the status and not
    /// the API tier's own account of what was wrong.
    /// </summary>
    [Fact]
    public async Task ASaveThatFailedBecomesAMessageAboutSaving()
    {
        var client = new PersonsClient(Transport(_ => Answer(
            HttpStatusCode.BadRequest,
            """{ "status": 400, "detail": "lastName is required: and so on." }""")));

        var saved = await client.Update(11, new UpdatePersonRequest(""));

        Assert.False(saved.Succeeded);
        Assert.Null(saved.Value);
        Assert.StartsWith(PersonsClient.UpdateFailurePrefix, saved.Failure);
        Assert.Contains("400", saved.Failure);
        Assert.DoesNotContain("lastName", saved.Failure, StringComparison.Ordinal);
    }

    /// <summary>
    /// The four things this client does fail differently, because a user who
    /// cannot search, one whose person could not be read, one whose person
    /// was not created and one whose record was not saved are being told four
    /// different things.
    /// </summary>
    [Fact]
    public async Task EachOfTheFourFailuresSaysWhichOneItWas()
    {
        var client = new PersonsClient(
            Transport(_ => Answer(HttpStatusCode.InternalServerError, "{}")));

        var searched = await client.Page(new PersonSearchCriteria(LastName: "Rod"), 1, 50);
        var found = await client.Find(11);
        var created = await client.Create(new CreatePersonRequest("Thorsen"));
        var saved = await client.Update(11, new UpdatePersonRequest("Thorsen"));

        Assert.StartsWith(PersonsClient.SearchFailurePrefix, searched.Failure);
        Assert.StartsWith(PersonsClient.FindFailurePrefix, found.Failure);
        Assert.StartsWith(PersonsClient.CreateFailurePrefix, created.Failure);
        Assert.StartsWith(PersonsClient.UpdateFailurePrefix, saved.Failure);

        Assert.Equal(
            4,
            new[] { searched.Failure, found.Failure, created.Failure, saved.Failure }
                .Distinct()
                .Count());
    }

    /// <summary>
    /// A screen reads this to tell "nothing typed" apart from "nothing
    /// found".
    /// </summary>
    [Fact]
    public void CriteriaNobodyTypedIntoAreEmpty()
    {
        Assert.True(new PersonSearchCriteria().IsEmpty);
        Assert.True(new PersonSearchCriteria(LastName: "  ").IsEmpty);
        Assert.False(new PersonSearchCriteria(LastName: "Rod").IsEmpty);
        Assert.False(new PersonSearchCriteria(BirthDate: new DateOnly(1985, 6, 15)).IsEmpty);
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
