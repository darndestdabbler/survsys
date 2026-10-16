using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The person round-trip over the real database: the whole vertical slice,
/// from the migration through the query and the mapping to the JSON a screen
/// will read — and, in
/// <see cref="APersonWrittenThroughTheSchemaComesBackWholeOverHttp"/>, back
/// the other way as well.
/// </summary>
/// <remarks>
/// The read half runs over the people <c>Fixtures/persons.sql</c> seeds, one
/// of whom is the real person
/// <c>../nedss/docs/db-snapshots/session-010/03-after-demographics-save.txt</c>
/// dumps. The write half writes a person of its own through
/// <see cref="SurvsysDbContext"/>, because this session adds no write
/// endpoint: creating a person is the legacy two-step flow's, which is a later
/// session's work. Writing through the context still proves what the round
/// trip is for — that what the schema stores is what the wire returns — and it
/// proves it against the schema the migration produces, which is the only
/// schema a deployment will ever get.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class PersonEndpointsTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task TheFirstPageCarriesTheSeededPeopleInNameOrder()
    {
        var page = await Persons("/api/persons");

        Assert.Equal(SqlServerFixture.PersonsByName, page.Items.Select(Named));
        Assert.Equal(SqlServerFixture.SeededPersons, page.TotalCount);
        Assert.Equal(1, page.Page);
        Assert.Equal(50, page.PageSize);
    }

    /// <summary>
    /// The pages partition the collection: every person once, in order, and
    /// nobody on two pages. This is what the ordering is for — an unordered
    /// query can return one row twice and another not at all, and still look
    /// right read one page at a time.
    /// </summary>
    [Fact]
    public async Task ThePagesPartitionTheCollectionInOrder()
    {
        var first = await Persons("/api/persons?page=1&pageSize=3");
        var second = await Persons("/api/persons?page=2&pageSize=3");
        var third = await Persons("/api/persons?page=3&pageSize=3");

        Assert.Equal(3, first.Items.Count);
        Assert.Equal(3, second.Items.Count);
        Assert.Single(third.Items);

        var paged = first.Items
            .Concat(second.Items)
            .Concat(third.Items)
            .Select(Named);

        Assert.Equal(SqlServerFixture.PersonsByName, paged);

        foreach (var page in new[] { first, second, third })
        {
            Assert.Equal(SqlServerFixture.SeededPersons, page.TotalCount);
            Assert.Equal(3, page.PageSize);
        }
    }

    [Fact]
    public async Task APagePastTheLastPersonIsEmptyRatherThanAnError()
    {
        var page = await Persons("/api/persons?page=99&pageSize=3");

        Assert.Empty(page.Items);
        Assert.Equal(SqlServerFixture.SeededPersons, page.TotalCount);
    }

    /// <summary>
    /// A list carries the seven fields it shows and no child row at all: the
    /// projection EF Core translates never reaches for an address, so a page
    /// of fifty people is a page of fifty rows.
    /// </summary>
    [Fact]
    public async Task AListedPersonCarriesNoAddressTelephoneOrEmail()
    {
        using var response = await api.CreateClient().GetAsync("/api/persons?pageSize=200");
        var body = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();

        Assert.DoesNotContain("1234 Main Street", body, StringComparison.Ordinal);
        Assert.DoesNotContain("5551234", body, StringComparison.Ordinal);
        Assert.DoesNotContain("maria.rodriguez@example.com", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// The real person, read back whole. Every value asserted here is one
    /// <c>03-after-demographics-save.txt</c> dumps, except the two races, the
    /// alias and the guardian, which the fixture says are written for this
    /// suite because the snapshots hold none.
    /// </summary>
    [Fact]
    public async Task TheSnapshotPersonCarriesEveryDemographicFieldAndEveryRowThatHangsOffHer()
    {
        var detail = await Person(await IdOf("Rodriguez, Maria"));

        Assert.Equal("Rodriguez", detail.LastName);
        Assert.Equal("Maria", detail.FirstName);
        Assert.Equal("Elena", detail.MiddleName);
        Assert.Equal(new DateOnly(1985, 6, 15), detail.BirthDate);
        Assert.Null(detail.DaysOldNoBirthday);
        Assert.Equal("F", detail.BirthGender);
        Assert.Equal("F", detail.CurrentGender);
        Assert.Equal("H", detail.Ethnicity);
        Assert.Equal("es", detail.Language);
        Assert.Equal("MEX", detail.CountryOfBirth);

        // The multi-select, ordered so that the assertion is about the
        // mapping and not about the order two rows happened to be written in.
        Assert.Equal(["AA", "W"], detail.Races);
        Assert.Equal(["Maria Elena Garcia"], detail.Aliases);
        Assert.Equal(["Carlos Rodriguez"], detail.ParentGuardians);

        var address = Assert.Single(detail.Addresses);
        Assert.Equal("1234 Main Street", address.Street);
        Assert.Equal("Apt 5B", address.UnitNumber);
        Assert.Equal("Salt Lake City", address.City);
        Assert.Equal("UT", address.State);
        Assert.Equal("84101", address.PostalCode);
        Assert.Equal("SL", address.County);
        Assert.Null(address.Beginning);
        Assert.Null(address.Ending);

        var telephone = Assert.Single(detail.Telephones);
        Assert.Equal("801", telephone.AreaCode);
        Assert.Equal("5551234", telephone.PhoneNumber);

        var email = Assert.Single(detail.Emails);
        Assert.Equal("maria.rodriguez@example.com", email.EmailAddress);
    }

    /// <summary>
    /// A person with only the one required name part comes back with nulls
    /// and empty collections rather than with an error: the legacy data is
    /// full of people known by a surname and nothing else.
    /// </summary>
    [Fact]
    public async Task APersonWithOnlyALastNameComesBackWithEmptyCollections()
    {
        var detail = await Person(await IdOf("Doe, "));

        Assert.Equal("Doe", detail.LastName);
        Assert.Null(detail.FirstName);
        Assert.Null(detail.BirthDate);
        Assert.Empty(detail.Races);
        Assert.Empty(detail.Aliases);
        Assert.Empty(detail.ParentGuardians);
        Assert.Empty(detail.Addresses);
        Assert.Empty(detail.Telephones);
        Assert.Empty(detail.Emails);
    }

    /// <summary>The legacy fallback for an age without a birthday.</summary>
    [Fact]
    public async Task AnInfantWithNoBirthdayCarriesTheirAgeInDays()
    {
        var detail = await Person(await IdOf("Abbott, Jane"));

        Assert.Null(detail.BirthDate);
        Assert.Equal(14, detail.DaysOldNoBirthday);
    }

    /// <summary>
    /// The round trip closed: a person written through the schema — with one
    /// of every row that hangs off a person — read back over HTTP field by
    /// field, and then deleted, which is where the cascade the configurations
    /// declare is proved.
    /// </summary>
    /// <remarks>
    /// The delete is in a <c>finally</c> so that a failed assertion still
    /// leaves the database as the fixture seeded it: the other tests in this
    /// suite assert the seeded count, and a person left behind would fail
    /// them instead of this one.
    /// </remarks>
    [Fact]
    public async Task APersonWrittenThroughTheSchemaComesBackWholeOverHttp()
    {
        var written = new Person
        {
            LastName = "Ramirez",
            FirstName = "Sofia",
            MiddleName = "Luz",
            BirthDate = new DateOnly(2001, 3, 9),
            BirthGender = "F",
            CurrentGender = "F",
            Ethnicity = "H",
            Language = "es",
            CountryOfBirth = "USA",
            Races = { new PersonRace { Race = "W" }, new PersonRace { Race = "B" } },
            Aliases = { new PersonAlias { Alias = "Sofia Luz Ramirez-Vega" } },
            Guardians = { new ParentGuardian { Name = "Elena Vega" } },
            Addresses =
            {
                new PersonAddress
                {
                    Street = "88 Orchard Lane",
                    UnitNumber = "2",
                    City = "Provo",
                    State = "UT",
                    PostalCode = "84604",
                    County = "UT",
                    Beginning = new DateOnly(2019, 8, 1),
                    Ending = new DateOnly(2024, 5, 31),
                    Note = "Moved out when the lease ended.",
                    ResidenceTypeId = 3,
                },
            },
            Telephones =
            {
                new PersonTelephone
                {
                    TelephoneType = "C",
                    CountryCode = "1",
                    AreaCode = "385",
                    PhoneNumber = "5559876",
                    Extension = "12",
                    Beginning = new DateOnly(2020, 1, 15),
                    Note = "Mobile, texts preferred.",
                },
            },
            Emails = { new PersonEmail { EmailAddress = "sofia@example.com", CurrentEmail = true } },
        };

        await using (var context = server.NewContext())
        {
            context.Persons.Add(written);
            await context.SaveChangesAsync();
        }

        try
        {
            var detail = await Person(written.Id);

            Assert.Equal("Ramirez", detail.LastName);
            Assert.Equal("Sofia", detail.FirstName);
            Assert.Equal("Luz", detail.MiddleName);
            Assert.Equal(new DateOnly(2001, 3, 9), detail.BirthDate);
            Assert.Equal("F", detail.BirthGender);
            Assert.Equal("F", detail.CurrentGender);
            Assert.Equal("H", detail.Ethnicity);
            Assert.Equal("es", detail.Language);
            Assert.Equal("USA", detail.CountryOfBirth);

            Assert.Equal(["B", "W"], detail.Races);
            Assert.Equal(["Sofia Luz Ramirez-Vega"], detail.Aliases);
            Assert.Equal(["Elena Vega"], detail.ParentGuardians);

            var address = Assert.Single(detail.Addresses);
            Assert.Equal("88 Orchard Lane", address.Street);
            Assert.Equal("2", address.UnitNumber);
            Assert.Equal("Provo", address.City);
            Assert.Equal("UT", address.State);
            Assert.Equal("84604", address.PostalCode);
            Assert.Equal("UT", address.County);
            Assert.Equal(new DateOnly(2019, 8, 1), address.Beginning);
            Assert.Equal(new DateOnly(2024, 5, 31), address.Ending);
            Assert.Equal("Moved out when the lease ended.", address.Note);
            Assert.Equal(3, address.ResidenceTypeId);

            var telephone = Assert.Single(detail.Telephones);
            Assert.Equal(
                new(telephone.Id, "C", "1", "385", "5559876", "12", new DateOnly(2020, 1, 15),
                    "Mobile, texts preferred."),
                telephone);

            var email = Assert.Single(detail.Emails);
            Assert.Equal("sofia@example.com", email.EmailAddress);
            Assert.True(email.CurrentEmail);
        }
        finally
        {
            await using var context = server.NewContext();

            await context.Persons
                .Where(person => person.Id == written.Id)
                .ExecuteDeleteAsync();
        }

        // Deleting the person took every row that hung off them with it,
        // which is what the cascade on each required foreign key says.
        await using var after = server.NewContext();

        Assert.Empty(await after.PersonAddresses
            .Where(address => address.PersonId == written.Id)
            .ToListAsync());
        Assert.Empty(await after.PersonTelephones
            .Where(telephone => telephone.PersonId == written.Id)
            .ToListAsync());
        Assert.Empty(await after.PersonEmails
            .Where(email => email.PersonId == written.Id)
            .ToListAsync());
        Assert.Empty(await after.PersonRaces
            .Where(race => race.PersonId == written.Id)
            .ToListAsync());
        Assert.Empty(await after.PersonAliases
            .Where(alias => alias.PersonId == written.Id)
            .ToListAsync());
        Assert.Empty(await after.ParentGuardians
            .Where(guardian => guardian.ChildId == written.Id)
            .ToListAsync());
    }

    [Fact]
    public async Task AnIdNobodyHasIsAProblemDetails()
    {
        using var response = await api.CreateClient().GetAsync("/api/persons/987654");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = problem.RootElement;

        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.Contains("No person has id 987654",
            root.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("correlationId").GetString()));
    }

    [Theory]
    [InlineData("?page=0", "page must be 1 or greater")]
    [InlineData("?pageSize=0", "pageSize must be 1 or greater")]
    [InlineData("?pageSize=201", "pageSize must be 200 or fewer")]
    public async Task APageOutsideItsBoundsIsAProblemDetails(string query, string expected)
    {
        using var response = await api.CreateClient().GetAsync("/api/persons" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = problem.RootElement;

        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Contains(expected, root.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("correlationId").GetString()));
    }

    /// <summary>
    /// An id that is not a number at all does not match the route's
    /// <c>{id:int}</c> constraint, so it is a 404 rather than a 500 — and
    /// Problem Details, like every other failure this tier returns.
    /// </summary>
    [Fact]
    public async Task AnIdThatIsNotANumberIsAlsoAProblemDetails()
    {
        using var response = await api.CreateClient().GetAsync("/api/persons/maria");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// A surname prefix: the criterion the search form is mostly used with.
    /// Two of the seeded people are Rodriguezes, and the matches come back in
    /// the order the endpoint promises rather than in the order the fixture
    /// inserted them.
    /// </summary>
    [Fact]
    public async Task ASurnamePrefixMatchesTheSurnamesThatStartWithIt()
    {
        var page = await Persons("/api/persons?lastName=Rod");

        Assert.Equal(["Rodriguez, Carlos", "Rodriguez, Maria"], page.Items.Select(Named));
        Assert.Equal(2, page.TotalCount);
    }

    /// <summary>
    /// The total is the total of what was searched for and not of the table —
    /// the count is taken over the filtered query, which is the mistake this
    /// asserts against: a pager that said seven here would offer pages that
    /// were not there.
    /// </summary>
    [Fact]
    public async Task ASearchIsPagedOverItsOwnMatchesAndNotOverTheTable()
    {
        var first = await Persons("/api/persons?lastName=Rodriguez&page=1&pageSize=1");
        var second = await Persons("/api/persons?lastName=Rodriguez&page=2&pageSize=1");

        Assert.Equal(["Rodriguez, Carlos"], first.Items.Select(Named));
        Assert.Equal(["Rodriguez, Maria"], second.Items.Select(Named));
        Assert.Equal(2, first.TotalCount);
        Assert.Equal(2, second.TotalCount);
    }

    [Fact]
    public async Task AFirstNamePrefixNarrowsASurnameToOnePerson()
    {
        var page = await Persons("/api/persons?lastName=Rodriguez&firstName=Mar");

        Assert.Equal(["Rodriguez, Maria"], page.Items.Select(Named));
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task AMiddleNamePrefixMatches()
    {
        var page = await Persons("/api/persons?middleName=Ele");

        Assert.Equal(["Rodriguez, Maria"], page.Items.Select(Named));
    }

    /// <summary>
    /// The case a user types in is not the case the name is stored in, and the
    /// search finds them anyway. SQL Server's doing, under the column's
    /// default collation — which is why this is asserted here, against a real
    /// database, and not in the unit suite where the same expression compiled
    /// in memory would be case-sensitive.
    /// </summary>
    [Theory]
    [InlineData("rodriguez")]
    [InlineData("RODRIGUEZ")]
    [InlineData("rOdRiGuEz")]
    public async Task ASurnameInTheWrongCaseMatchesAllTheSame(string typed)
    {
        var page = await Persons($"/api/persons?lastName={typed}");

        Assert.Equal(["Rodriguez, Carlos", "Rodriguez, Maria"], page.Items.Select(Named));
    }

    /// <summary>
    /// The date of birth matches exactly, and not as a prefix or a range: a
    /// person born the day before is not a match for the day after.
    /// </summary>
    [Fact]
    public async Task ADateOfBirthMatchesExactly()
    {
        var matched = await Persons("/api/persons?birthDate=1985-06-15");
        var missed = await Persons("/api/persons?birthDate=1985-06-16");

        Assert.Equal(["Rodriguez, Maria"], matched.Items.Select(Named));
        Assert.Empty(missed.Items);
    }

    [Fact]
    public async Task ABirthGenderMatchesTheCodeItIsStoredAs()
    {
        var page = await Persons("/api/persons?birthGender=F");

        Assert.Equal(["Abbott, Jane", "Rodriguez, Maria"], page.Items.Select(Named));
        Assert.Equal(2, page.TotalCount);
    }

    /// <summary>
    /// One telephone number, typed the ways a person types one — whole, in
    /// punctuation, and without its area code — against the two columns the
    /// legacy schema stores it in. The one seeded telephone is Maria's, area
    /// code <c>801</c> and number <c>5551234</c>, as
    /// <c>03-after-demographics-save.txt</c> dumps it.
    /// </summary>
    [Theory]
    [InlineData("8015551234")]
    [InlineData("(801) 555-1234")]
    [InlineData("801-555-1234")]
    [InlineData("555-1234")]
    [InlineData("5551234")]
    [InlineData("801")]
    public async Task ATelephoneMatchesOnItsDigitsHoweverItWasTyped(string typed)
    {
        var page = await Persons($"/api/persons?phone={Uri.EscapeDataString(typed)}");

        Assert.Equal(["Rodriguez, Maria"], page.Items.Select(Named));
        Assert.Equal(1, page.TotalCount);
    }

    /// <summary>
    /// A telephone stored with its country code, searched for the way a person
    /// types one: <c>+1 (801) 555-1234</c> is eleven digits, and they are the
    /// three stored columns in the order they are dialled.
    /// </summary>
    /// <remarks>
    /// The row is written here rather than seeded, because
    /// <c>Fixtures/persons.sql</c> copies the one telephone the legacy
    /// snapshot dumps and that row has no country code — the snapshot's
    /// <c>country_code</c> is empty. Writing it through the context keeps the
    /// fixture the snapshot's and still proves the query against the schema
    /// the migration produces; the person is taken back out afterwards, as
    /// every person this suite writes is.
    /// </remarks>
    [Theory]
    [InlineData("+1 (801) 555-1234")]
    [InlineData("1-801-555-1234")]
    [InlineData("18015551234")]
    [InlineData("8015551234")]
    [InlineData("5551234")]
    public async Task ATelephoneStoredWithACountryCodeIsFoundByTheWholeNumber(string typed)
    {
        var written = new Person
        {
            LastName = "Ferreira",
            FirstName = "Tomas",
            Telephones =
            {
                new PersonTelephone
                {
                    CountryCode = "1",
                    AreaCode = "801",
                    PhoneNumber = "5551234",
                },
            },
        };

        await using (var context = server.NewContext())
        {
            context.Persons.Add(written);
            await context.SaveChangesAsync();
        }

        try
        {
            var page = await Persons($"/api/persons?phone={Uri.EscapeDataString(typed)}");

            Assert.Contains("Ferreira, Tomas", page.Items.Select(Named));
        }
        finally
        {
            await Forget(written.Id);
        }
    }

    [Fact]
    public async Task ATelephoneNobodyHasMatchesNobody()
    {
        var page = await Persons("/api/persons?phone=9999999");

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    /// <summary>
    /// Criteria narrow each other rather than widening the search: Carlos
    /// matches the surname and the gender, and Maria matches only the surname.
    /// </summary>
    [Fact]
    public async Task TwoCriteriaTogetherMatchOnlyWhatMatchesBoth()
    {
        var page = await Persons("/api/persons?lastName=Rod&birthGender=M");

        Assert.Equal(["Rodriguez, Carlos"], page.Items.Select(Named));
        Assert.Equal(1, page.TotalCount);
    }

    /// <summary>
    /// A search that found nobody is an empty page and not a 404: the
    /// collection was found, and it has nothing in it that matches.
    /// </summary>
    [Fact]
    public async Task ASearchThatMatchesNobodyIsAnEmptyPage()
    {
        using var response = await api.CreateClient().GetAsync("/api/persons?lastName=Zzyzx");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = (await response.Content.ReadFromJsonAsync<PagedResult<PersonResponse>>(Json))!;

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    /// <summary>
    /// A screen whose search boxes were left empty: every text criterion
    /// present and every one blank. It is the same request as no criteria at
    /// all, which is the unfiltered page this endpoint returned before search
    /// existed — and the existing tests above are the rest of that promise.
    /// </summary>
    /// <remarks>
    /// The date of birth is not among them, and
    /// <see cref="ADateOfBirthThatIsNotADateIsAProblemDetails"/> says why: it
    /// is the one typed criterion, so an empty value for it is a value that is
    /// not a date rather than a criterion nobody gave. A caller leaves it out
    /// of the query string instead, which is what <c>Survsys.Web</c>'s client
    /// does with every criterion nobody typed.
    /// </remarks>
    [Fact]
    public async Task CriteriaThatAreAllBlankLeaveThePageUnfiltered()
    {
        var page = await Persons(
            "/api/persons?lastName=&firstName=&middleName=&phone=&birthGender=");

        Assert.Equal(SqlServerFixture.PersonsByName, page.Items.Select(Named));
        Assert.Equal(SqlServerFixture.SeededPersons, page.TotalCount);
    }

    /// <summary>
    /// A surname that is nothing but spaces is a surname nobody typed, rather
    /// than a search for names beginning with a space.
    /// </summary>
    [Fact]
    public async Task ASurnameOfSpacesLeavesThePageUnfiltered()
    {
        var page = await Persons("/api/persons?lastName=%20%20");

        Assert.Equal(SqlServerFixture.SeededPersons, page.TotalCount);
    }

    /// <summary>
    /// The search is a filter and not a pattern language: a value carrying
    /// SQL Server's own wildcards matches the people whose names contain those
    /// characters, which is nobody, rather than matching everybody.
    /// </summary>
    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("[R]")]
    public async Task ASurnameCarryingAWildcardMatchesNobodyRatherThanEverybody(string typed)
    {
        var page = await Persons($"/api/persons?lastName={Uri.EscapeDataString(typed)}");

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    /// <summary>
    /// A date of birth that is not a date is a 400 rather than a 500 or a
    /// search for everybody: the framework refuses to bind it, and
    /// <c>UseStatusCodePages</c> turns that into the one error shape this tier
    /// returns.
    /// </summary>
    /// <remarks>
    /// An empty value is one of them, which is the one asymmetry in these
    /// criteria and is on the record here rather than left to be discovered: a
    /// blank text criterion is a criterion nobody gave, but
    /// <c>?birthDate=</c> is a date this tier cannot read, because the date is
    /// bound and parsed by the framework before any of this tier's own code
    /// runs. A criterion nobody typed is left out of the query string
    /// altogether, which is what <c>Survsys.Web</c>'s client does with every
    /// one of them.
    /// </remarks>
    [Theory]
    [InlineData("yesterday")]
    [InlineData("1985-13-45")]
    [InlineData("")]
    public async Task ADateOfBirthThatIsNotADateIsAProblemDetails(string typed)
    {
        using var response = await api.CreateClient().GetAsync($"/api/persons?birthDate={typed}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// The creation the legacy flow's "Create Person and CMR" button makes: a
    /// person who was not on file is put on file, read back from the
    /// <c>Location</c> the 201 gave, and then found by the search that failed
    /// to find them a moment ago.
    /// </summary>
    [Fact]
    public async Task APersonPostedIsCreatedReadableAndThenFindable()
    {
        var client = api.CreateClient();

        var before = await Persons("/api/persons?lastName=Thorsen");
        Assert.Empty(before.Items);

        using var created = await client.PostAsJsonAsync("/api/persons", new CreatePersonRequest(
            LastName: "Thorsen",
            FirstName: "Inge",
            MiddleName: "Birgit",
            BirthDate: new DateOnly(1971, 3, 9),
            BirthGender: "F"), Json);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var posted = (await created.Content.ReadFromJsonAsync<PersonResponse>(Json))!;

        try
        {
            Assert.True(posted.Id > 0);
            Assert.Equal("Thorsen", posted.LastName);
            Assert.Equal("Inge", posted.FirstName);
            Assert.Equal("Birgit", posted.MiddleName);
            Assert.Equal(new DateOnly(1971, 3, 9), posted.BirthDate);
            Assert.Equal("F", posted.BirthGender);

            // The Location is an endpoint that exists, and it reads back the
            // person just created rather than a second person who happens to
            // share their name.
            Assert.Equal($"/api/persons/{posted.Id}", created.Headers.Location?.ToString());

            using var read = await client.GetAsync(created.Headers.Location);
            read.EnsureSuccessStatusCode();
            var detail = (await read.Content.ReadFromJsonAsync<PersonDetailResponse>(Json))!;

            Assert.Equal(posted.Id, detail.Id);
            Assert.Equal("Thorsen", detail.LastName);
            Assert.Equal("Inge", detail.FirstName);
            Assert.Equal("Birgit", detail.MiddleName);
            Assert.Equal(new DateOnly(1971, 3, 9), detail.BirthDate);
            Assert.Equal("F", detail.BirthGender);

            var after = await Persons("/api/persons?lastName=Thorsen");
            Assert.Equal(["Thorsen, Inge"], after.Items.Select(Named));
        }
        finally
        {
            await Forget(posted.Id);
        }
    }

    /// <summary>
    /// A person created from a search form has nothing but what was typed:
    /// no address, telephone, email, race, alias or guardian, and no
    /// demographic field the request does not carry. Filling those in is the
    /// Demographics screen's, which is a later session's.
    /// </summary>
    [Fact]
    public async Task APersonPostedWithNothingButALastNameIsCreatedWithNothingElse()
    {
        using var created = await api.CreateClient()
            .PostAsJsonAsync("/api/persons", new CreatePersonRequest("Kettleborough"), Json);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var posted = (await created.Content.ReadFromJsonAsync<PersonResponse>(Json))!;

        try
        {
            var detail = await Person(posted.Id);

            Assert.Equal("Kettleborough", detail.LastName);
            Assert.Null(detail.FirstName);
            Assert.Null(detail.MiddleName);
            Assert.Null(detail.BirthDate);
            Assert.Null(detail.DaysOldNoBirthday);
            Assert.Null(detail.BirthGender);
            Assert.Null(detail.CurrentGender);
            Assert.Null(detail.Ethnicity);
            Assert.Null(detail.Language);
            Assert.Null(detail.CountryOfBirth);
            Assert.Empty(detail.Races);
            Assert.Empty(detail.Aliases);
            Assert.Empty(detail.ParentGuardians);
            Assert.Empty(detail.Addresses);
            Assert.Empty(detail.Telephones);
            Assert.Empty(detail.Emails);
        }
        finally
        {
            await Forget(posted.Id);
        }
    }

    /// <summary>
    /// What a form sends when a box was typed in with spaces, or submitted
    /// empty: the name is stored trimmed, and a blank field is stored as
    /// nothing said rather than as an empty string.
    /// </summary>
    [Fact]
    public async Task APersonPostedWithSpacesAndBlanksIsStoredTrimmedAndNull()
    {
        using var created = await api.CreateClient().PostAsJsonAsync(
            "/api/persons",
            new CreatePersonRequest(
                LastName: "  Pemberton ",
                FirstName: " Ada",
                MiddleName: "  ",
                BirthDate: null,
                BirthGender: ""),
            Json);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var posted = (await created.Content.ReadFromJsonAsync<PersonResponse>(Json))!;

        try
        {
            var detail = await Person(posted.Id);

            Assert.Equal("Pemberton", detail.LastName);
            Assert.Equal("Ada", detail.FirstName);
            Assert.Null(detail.MiddleName);
            Assert.Null(detail.BirthGender);
        }
        finally
        {
            await Forget(posted.Id);
        }
    }

    /// <summary>
    /// The one requirement the schema has, refused before anything is written:
    /// the Problem Details names the field, and the person count is what it
    /// was.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task APersonWithoutALastNameIsAProblemDetailsAndIsNotWritten(string? lastName)
    {
        var before = await Persons("/api/persons?pageSize=200");

        using var response = await api.CreateClient()
            .PostAsJsonAsync("/api/persons", new CreatePersonRequest(lastName), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = problem.RootElement;

        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Contains("lastName is required",
            root.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("correlationId").GetString()));

        var after = await Persons("/api/persons?pageSize=200");
        Assert.Equal(before.TotalCount, after.TotalCount);
    }

    /// <summary>
    /// A body that is not a person at all. The framework refuses to bind it
    /// and <c>UseStatusCodePages</c> turns that into the one error shape this
    /// tier returns, so a caller reads one shape whether the request was
    /// wrong or the body was.
    /// </summary>
    [Fact]
    public async Task ABodyThatIsNotJsonIsAProblemDetails()
    {
        using var body = new StringContent(
            "last name please", System.Text.Encoding.UTF8, "application/json");

        using var response = await api.CreateClient().PostAsync("/api/persons", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// How <see cref="SqlServerFixture.PersonsByName"/> writes a person: the
    /// endpoint promises an order over two name parts, and a person with no
    /// first name is one of them.
    /// </summary>
    private static string Named(PersonResponse person) =>
        $"{person.LastName}, {person.FirstName}";

    /// <summary>
    /// Takes a person this suite created back out of the database, so that the
    /// tests asserting <see cref="SqlServerFixture.SeededPersons"/> are
    /// asserting the fixture and not whichever creation test ran first.
    /// </summary>
    /// <remarks>
    /// Through the context, because this tier has no delete endpoint and is
    /// not getting one: a test cleans up with the schema and not with a
    /// feature nobody asked for.
    /// </remarks>
    private async Task Forget(int personId)
    {
        await using var context = server.NewContext();

        await context.Persons
            .Where(person => person.Id == personId)
            .ExecuteDeleteAsync();
    }

    private async Task<int> IdOf(string named)
    {
        var page = await Persons("/api/persons?pageSize=200");

        return page.Items.Single(person => Named(person) == named).Id;
    }

    private async Task<PagedResult<PersonResponse>> Persons(string url)
    {
        using var response = await api.CreateClient().GetAsync(url);

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PagedResult<PersonResponse>>(Json))!;
    }

    private async Task<PersonDetailResponse> Person(int id)
    {
        using var response = await api.CreateClient().GetAsync($"/api/persons/{id}");

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PersonDetailResponse>(Json))!;
    }
}
