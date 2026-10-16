using System.Linq.Expressions;
using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the persons endpoints make of a stored person: the list mapping and
/// the detail mapping, both without a database and without a host — what needs
/// either is the integration suite's.
/// </summary>
/// <remarks>
/// Both are expression trees, and both are proved by compiling and invoking
/// the very expression EF Core translates, rather than a method written beside
/// it that could drift from it.
///
/// The page bounds these endpoints answer by are <c>Paging</c>'s, and
/// <see cref="PagingTests"/> proves them there.
/// </remarks>
public class PersonEndpointsTests
{
    /// <summary>
    /// Maria Elena Rodriguez, as
    /// <c>../nedss/docs/db-snapshots/session-010/03-after-demographics-save.txt</c>
    /// dumps her, with a race, an alias and a guardian added — the snapshot
    /// has none of those three, and the mappings have to carry them.
    /// </summary>
    private static Person Seeded() => new()
    {
        Id = 11,
        LastName = "Rodriguez",
        FirstName = "Maria",
        MiddleName = "Elena",
        BirthDate = new DateOnly(1985, 6, 15),
        DaysOldNoBirthday = null,
        BirthGender = "F",
        CurrentGender = "F",
        Ethnicity = "H",
        Language = "es",
        CountryOfBirth = "MEX",
        Races = { new PersonRace { PersonId = 11, Race = "W" } },
        Aliases = { new PersonAlias { PersonId = 11, Alias = "Maria Elena Garcia" } },
        Guardians = { new ParentGuardian { Id = 4, ChildId = 11, Name = "Carlos Rodriguez" } },
        Addresses =
        {
            new PersonAddress
            {
                Id = 11,
                PersonId = 11,
                Street = "1234 Main Street",
                UnitNumber = "Apt 5B",
                City = "Salt Lake City",
                State = "UT",
                PostalCode = "84101",
                County = "SL",
                Beginning = null,
                Ending = null,
                Note = null,
                ResidenceTypeId = null,
            },
        },
        Telephones =
        {
            new PersonTelephone
            {
                Id = 11,
                PersonId = 11,
                AreaCode = "801",
                PhoneNumber = "5551234",
            },
        },
        Emails =
        {
            new PersonEmail
            {
                Id = 11,
                PersonId = 11,
                EmailAddress = "maria.rodriguez@example.com",
            },
        },
    };

    [Fact]
    public void ToResponseCarriesTheSevenFieldsAListShows()
    {
        var response = PersonEndpoints.ToResponse.Compile().Invoke(Seeded());

        Assert.Equal(
            new(11, "Rodriguez", "Maria", "Elena", new DateOnly(1985, 6, 15), "F", "F"),
            response);
    }

    /// <summary>
    /// A list reads seven columns and no child row at all. Read off the
    /// expression tree, because that is the thing EF Core translates into a
    /// <c>SELECT</c> list: a list of fifty people that reached for their
    /// addresses would be reading rows for a screen that shows none of them.
    /// </summary>
    [Fact]
    public void ToResponseReadsNoColumnItDoesNotPutOnTheWire()
    {
        var construction = Assert.IsAssignableFrom<NewExpression>(PersonEndpoints.ToResponse.Body);

        var read = construction.Arguments
            .Cast<MemberExpression>()
            .Select(argument => argument.Member.Name)
            .ToList();

        Assert.Equal(
            [
                nameof(Person.Id),
                nameof(Person.LastName),
                nameof(Person.FirstName),
                nameof(Person.MiddleName),
                nameof(Person.BirthDate),
                nameof(Person.BirthGender),
                nameof(Person.CurrentGender),
            ],
            read);
    }

    [Fact]
    public void ToDetailCarriesEveryDemographicFieldOfThePerson()
    {
        var detail = PersonEndpoints.ToDetail.Compile().Invoke(Seeded());

        Assert.Equal(11, detail.Id);
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
    }

    [Fact]
    public void ToDetailCarriesTheRowsThatHangOffThePerson()
    {
        var detail = PersonEndpoints.ToDetail.Compile().Invoke(Seeded());

        Assert.Equal(["W"], detail.Races);
        Assert.Equal(["Maria Elena Garcia"], detail.Aliases);
        Assert.Equal(["Carlos Rodriguez"], detail.ParentGuardians);

        var address = Assert.Single(detail.Addresses);
        Assert.Equal(
            new(11, "1234 Main Street", "Apt 5B", "Salt Lake City", "UT", "84101", "SL",
                null, null, null, null),
            address);

        var telephone = Assert.Single(detail.Telephones);
        Assert.Equal(new(11, null, null, "801", "5551234", null, null, null), telephone);

        var email = Assert.Single(detail.Emails);
        Assert.Equal(new(11, "maria.rodriguez@example.com", null), email);
    }

    /// <summary>
    /// A guardian row with no name carries only a link to another person, and
    /// a list of names has nothing to show for it. What the detail response
    /// says of itself, asserted.
    /// </summary>
    [Fact]
    public void ToDetailLeavesOutAGuardianWithNoName()
    {
        var person = Seeded();
        person.Guardians.Add(new ParentGuardian { Id = 5, ChildId = 11, ParentId = 12 });

        var detail = PersonEndpoints.ToDetail.Compile().Invoke(person);

        Assert.Equal(["Carlos Rodriguez"], detail.ParentGuardians);
    }

    /// <summary>
    /// A person with nothing but the one required name: every collection is
    /// empty rather than null, so a screen reads a length and not a null
    /// check, and every unknown field is null rather than an empty string.
    /// </summary>
    [Fact]
    public void ToDetailCarriesAPersonWithNothingButALastName()
    {
        var detail = PersonEndpoints.ToDetail.Compile().Invoke(new Person { Id = 2, LastName = "Doe" });

        Assert.Equal(2, detail.Id);
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

    /// <summary>
    /// The created person is described by the list's own mapping, compiled —
    /// so what a creation answers with and what a list shows cannot drift
    /// apart.
    /// </summary>
    [Fact]
    public void RespondIsTheListMappingItself()
    {
        var person = Seeded();

        Assert.Equal(
            PersonEndpoints.ToResponse.Compile().Invoke(person),
            PersonEndpoints.Respond(person));
    }

    [Fact]
    public void WhyNotAPersonAcceptsAPersonWithALastName()
    {
        Assert.Null(PersonEndpoints.WhyNotAPerson(new CreatePersonRequest("Rodriguez")));
    }

    /// <summary>
    /// The one requirement the schema has, so the one rule this endpoint keeps
    /// — and it is kept before any query, which is what makes it provable
    /// here.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void WhyNotAPersonRefusesAPersonWithoutALastName(string? lastName)
    {
        Assert.Contains("lastName is required",
            PersonEndpoints.WhyNotAPerson(new CreatePersonRequest(lastName)),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Nothing else is required, and nothing else is checked: the legacy data
    /// leaves every other field empty on plenty of people, and the reference
    /// tables the codes belong to do not exist to check a code against.
    /// </summary>
    [Fact]
    public void WhyNotAPersonAcceptsAPersonWhoIsNothingButALastName()
    {
        Assert.Null(PersonEndpoints.WhyNotAPerson(new CreatePersonRequest(
            LastName: "Doe",
            FirstName: null,
            MiddleName: null,
            BirthDate: null,
            BirthGender: null)));
    }

    [Fact]
    public void ToPersonCarriesTheFiveFieldsARequestHolds()
    {
        var person = PersonEndpoints.ToPerson(new CreatePersonRequest(
            LastName: "Rodriguez",
            FirstName: "Maria",
            MiddleName: "Elena",
            BirthDate: new DateOnly(1985, 6, 15),
            BirthGender: "F"));

        Assert.Equal("Rodriguez", person.LastName);
        Assert.Equal("Maria", person.FirstName);
        Assert.Equal("Elena", person.MiddleName);
        Assert.Equal(new DateOnly(1985, 6, 15), person.BirthDate);
        Assert.Equal("F", person.BirthGender);
    }

    /// <summary>
    /// A person created from a search form has nothing else yet, and
    /// everything that is not asked for is unset rather than empty: a created
    /// person is a person nothing has been said about beyond their name.
    /// </summary>
    [Fact]
    public void ToPersonLeavesEverythingTheRequestDoesNotCarryUnset()
    {
        var person = PersonEndpoints.ToPerson(new CreatePersonRequest("Doe"));

        Assert.Equal(0, person.Id);
        Assert.Null(person.FirstName);
        Assert.Null(person.MiddleName);
        Assert.Null(person.BirthDate);
        Assert.Null(person.DaysOldNoBirthday);
        Assert.Null(person.BirthGender);
        Assert.Null(person.CurrentGender);
        Assert.Null(person.Ethnicity);
        Assert.Null(person.Language);
        Assert.Null(person.CountryOfBirth);
        Assert.Empty(person.Addresses);
        Assert.Empty(person.Telephones);
        Assert.Empty(person.Emails);
        Assert.Empty(person.Races);
        Assert.Empty(person.Aliases);
        Assert.Empty(person.Guardians);
    }

    /// <summary>
    /// What a form in front of a user sends: values with spaces around them,
    /// and boxes nobody typed in submitted as empty strings. A name is trimmed
    /// and a blank field is nothing said — NULL, which is how a column here
    /// says nobody has said.
    /// </summary>
    [Fact]
    public void ToPersonTrimsWhatWasTypedAndTreatsABlankFieldAsNothingSaid()
    {
        var person = PersonEndpoints.ToPerson(new CreatePersonRequest(
            LastName: "  Rodriguez ",
            FirstName: " Maria",
            MiddleName: "   ",
            BirthDate: null,
            BirthGender: ""));

        Assert.Equal("Rodriguez", person.LastName);
        Assert.Equal("Maria", person.FirstName);
        Assert.Null(person.MiddleName);
        Assert.Null(person.BirthGender);
    }
}
