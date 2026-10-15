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
    /// How <see cref="SqlServerFixture.PersonsByName"/> writes a person: the
    /// endpoint promises an order over two name parts, and a person with no
    /// first name is one of them.
    /// </summary>
    private static string Named(PersonResponse person) =>
        $"{person.LastName}, {person.FirstName}";

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
