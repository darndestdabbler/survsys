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
    /// <summary>The day every save in this file is made on.</summary>
    private static readonly DateOnly Today = new(2026, 10, 19);

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
        var person = Seeded();
        person.Addresses.Single().CountyEquivalent = "09110";

        var detail = PersonEndpoints.ToDetail.Compile().Invoke(person);

        Assert.Equal(["W"], detail.Races);
        Assert.Equal(["Maria Elena Garcia"], detail.Aliases);
        Assert.Equal(["Carlos Rodriguez"], detail.ParentGuardians);

        var address = Assert.Single(detail.Addresses);
        Assert.Equal(
            new(11, "1234 Main Street", "Apt 5B", "Salt Lake City", "UT", "84101", "SL",
                null, null, null, null, "09110"),
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

    /// <summary>
    /// A whole record, as the Demographic tab would send it over the seeded
    /// person: different in every field, so that a field the save left alone
    /// would show.
    /// </summary>
    private static UpdatePersonRequest Rewritten() => new(
        LastName: "Garcia",
        FirstName: "Mariela",
        MiddleName: "Luz",
        BirthDate: new DateOnly(1986, 7, 16),
        DaysOldNoBirthday: 12,
        BirthGender: "F",
        CurrentGender: "U",
        Ethnicity: "NH",
        Language: "en",
        CountryOfBirth: "USA")
    {
        Races = ["B", "AA"],
        Aliases = ["Mariela Garcia"],
        ParentGuardians = ["Ana Garcia", "Luis Garcia"],
        Addresses =
        [
            new PersonAddressRequest(
                Street: "9 Elm Court",
                UnitNumber: null,
                City: "Ogden",
                State: "UT",
                PostalCode: "84401",
                County: "WE",
                Beginning: new DateOnly(2020, 1, 2),
                Ending: null,
                Note: "Current.",
                ResidenceTypeId: 2),
        ],
        Telephones =
        [
            new PersonTelephoneRequest(
                TelephoneType: "C",
                CountryCode: "1",
                AreaCode: "385",
                PhoneNumber: "5550100",
                Extension: "7",
                Beginning: new DateOnly(2021, 2, 3),
                Note: "Mobile."),
        ],
        Emails = [new PersonEmailRequest("mariela@example.com", true)],
    };

    [Fact]
    public void WhyNotAPersonAcceptsARecordWithALastName()
    {
        Assert.Null(PersonEndpoints.WhyNotAPerson(new UpdatePersonRequest("Rodriguez")));
    }

    /// <summary>
    /// The same one rule the creation keeps, and the same sentence, because a
    /// save that took the last name away would leave a person the schema
    /// refuses.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void WhyNotAPersonRefusesARecordWithoutALastName(string? lastName)
    {
        Assert.Equal(
            PersonEndpoints.WhyNotAPerson(new CreatePersonRequest(lastName)),
            PersonEndpoints.WhyNotAPerson(new UpdatePersonRequest(lastName)));
        Assert.Contains("lastName is required",
            PersonEndpoints.WhyNotAPerson(new UpdatePersonRequest(lastName)),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyCarriesEveryFieldOfTheRecord()
    {
        var person = Seeded();

        PersonEndpoints.Apply(person, Rewritten(), Today);

        Assert.Equal(11, person.Id);
        Assert.Equal("Garcia", person.LastName);
        Assert.Equal("Mariela", person.FirstName);
        Assert.Equal("Luz", person.MiddleName);
        Assert.Equal(new DateOnly(1986, 7, 16), person.BirthDate);
        Assert.Equal(12, person.DaysOldNoBirthday);
        Assert.Equal("F", person.BirthGender);
        Assert.Equal("U", person.CurrentGender);
        Assert.Equal("NH", person.Ethnicity);
        Assert.Equal("en", person.Language);
        Assert.Equal("USA", person.CountryOfBirth);
    }

    /// <summary>
    /// The rows that hang off the person become the rows the request carries
    /// — none of the seeded address, telephone, email, race, alias or guardian
    /// survives, and each new row carries every field it was sent with.
    /// </summary>
    [Fact]
    public void ApplyReplacesEachChildCollectionWithWhatTheRecordCarries()
    {
        var person = Seeded();

        PersonEndpoints.Apply(person, Rewritten(), Today);

        var address = Assert.Single(person.Addresses);
        Assert.Equal(0, address.Id);
        Assert.Equal("9 Elm Court", address.Street);
        Assert.Null(address.UnitNumber);
        Assert.Equal("Ogden", address.City);
        Assert.Equal("UT", address.State);
        Assert.Equal("84401", address.PostalCode);
        Assert.Equal("WE", address.County);
        Assert.Equal(new DateOnly(2020, 1, 2), address.Beginning);
        Assert.Null(address.Ending);
        Assert.Equal("Current.", address.Note);
        Assert.Equal(2, address.ResidenceTypeId);

        var telephone = Assert.Single(person.Telephones);
        Assert.Equal(0, telephone.Id);
        Assert.Equal("C", telephone.TelephoneType);
        Assert.Equal("1", telephone.CountryCode);
        Assert.Equal("385", telephone.AreaCode);
        Assert.Equal("5550100", telephone.PhoneNumber);
        Assert.Equal("7", telephone.Extension);
        Assert.Equal(new DateOnly(2021, 2, 3), telephone.Beginning);
        Assert.Equal("Mobile.", telephone.Note);

        var email = Assert.Single(person.Emails);
        Assert.Equal(0, email.Id);
        Assert.Equal("mariela@example.com", email.EmailAddress);
        Assert.True(email.CurrentEmail);

        Assert.Equal(["B", "AA"], person.Races.Select(race => race.Race));
        Assert.Equal(["Mariela Garcia"], person.Aliases.Select(alias => alias.Alias));
        Assert.Equal(
            ["Ana Garcia", "Luis Garcia"],
            person.Guardians.Select(guardian => guardian.Name));
    }

    /// <summary>
    /// A record with every collection empty leaves the person with no rows at
    /// all: a row taken off the screen is simply absent from the next save.
    /// </summary>
    [Fact]
    public void ApplyWithEmptyCollectionsLeavesNoChildRow()
    {
        var person = Seeded();

        PersonEndpoints.Apply(person, new UpdatePersonRequest("Rodriguez"), Today);

        Assert.Empty(person.Addresses);
        Assert.Empty(person.Telephones);
        Assert.Empty(person.Emails);
        Assert.Empty(person.Races);
        Assert.Empty(person.Aliases);
        Assert.Empty(person.Guardians);
    }

    /// <summary>
    /// A race or an alias the person already has is the same row after the
    /// save, not a deleted one and an inserted one: those two tables are keyed
    /// by the value, and the same key deleted and inserted in one unit of
    /// work is one key tracked twice.
    /// </summary>
    [Fact]
    public void ApplyKeepsARaceOrAliasThePersonAlreadyHasRatherThanRewritingIt()
    {
        var person = Seeded();
        var race = person.Races.Single();
        var alias = person.Aliases.Single();

        PersonEndpoints.Apply(person, new UpdatePersonRequest("Rodriguez")
        {
            Races = ["AA", "W"],
            Aliases = ["Maria Elena Garcia", "M. E. Rodriguez"],
        }, Today);

        Assert.Contains(race, person.Races);
        Assert.Contains(alias, person.Aliases);
        Assert.Equal(["W", "AA"], person.Races.Select(held => held.Race));
        Assert.Equal(
            ["Maria Elena Garcia", "M. E. Rodriguez"],
            person.Aliases.Select(held => held.Alias));
    }

    /// <summary>
    /// A guardian row that holds only a link to another person is not shown
    /// by the detail and not sent back by the screen, so a save leaves it
    /// where it was; only the named rows are replaced.
    /// </summary>
    [Fact]
    public void ApplyLeavesAGuardianWithNoNameAlone()
    {
        var person = Seeded();
        var linked = new ParentGuardian { Id = 5, ChildId = 11, ParentId = 12 };
        person.Guardians.Add(linked);

        PersonEndpoints.Apply(person, new UpdatePersonRequest("Rodriguez")
        {
            ParentGuardians = ["Ana Garcia"],
        }, Today);

        Assert.Equal(2, person.Guardians.Count);
        Assert.Contains(linked, person.Guardians);
        Assert.Equal(["Ana Garcia"], person.Guardians
            .Where(guardian => guardian.Name != null)
            .Select(guardian => guardian.Name));
    }

    /// <summary>
    /// Every box the Demographic tab has can be submitted with spaces around
    /// what was typed, or empty: a value is trimmed, a blank one is nothing
    /// said, and a code typed twice into the multi-select is one code.
    /// </summary>
    [Fact]
    public void ApplyTrimsWhatWasTypedAndTreatsABlankFieldAsNothingSaid()
    {
        var person = Seeded();

        PersonEndpoints.Apply(person, new UpdatePersonRequest(
            LastName: "  Garcia ",
            FirstName: " Mariela",
            MiddleName: "   ",
            CurrentGender: "",
            Language: " en ")
        {
            Races = [" B", "B", "", "  "],
            Aliases = [" Mariela ", "\t"],
            ParentGuardians = ["", " Ana Garcia "],
            Addresses = [new PersonAddressRequest(Street: " 9 Elm Court ", City: "")],
            Telephones = [new PersonTelephoneRequest(AreaCode: " 385 ", PhoneNumber: " ")],
            Emails = [new PersonEmailRequest(EmailAddress: "  ")],
        }, Today);

        Assert.Equal("Garcia", person.LastName);
        Assert.Equal("Mariela", person.FirstName);
        Assert.Null(person.MiddleName);
        Assert.Null(person.CurrentGender);
        Assert.Equal("en", person.Language);

        Assert.Equal(["B"], person.Races.Select(race => race.Race));
        Assert.Equal(["Mariela"], person.Aliases.Select(alias => alias.Alias));
        Assert.Equal(["Ana Garcia"], person.Guardians.Select(guardian => guardian.Name));

        var address = Assert.Single(person.Addresses);
        Assert.Equal("9 Elm Court", address.Street);
        Assert.Null(address.City);

        var telephone = Assert.Single(person.Telephones);
        Assert.Equal("385", telephone.AreaCode);
        Assert.Null(telephone.PhoneNumber);

        Assert.Null(Assert.Single(person.Emails).EmailAddress);
    }

    [Fact]
    public void ToDetailCarriesTheIdentifiersAndTheFormerNamesLatestFirst()
    {
        var person = Seeded();
        person.Identifiers.Add(new PersonIdentifier
        {
            Id = 3,
            IdentifierType = "MR",
            Value = "A-100",
            AssigningAuthority = "46D0523979",
            AssigningAuthorityType = PersonIdentifier.Clia,
        });
        person.NameHistory.Add(new PersonNameHistory
        {
            Id = 1, LastName = "Lopez", FirstName = "Maria", ChangedOn = new DateOnly(2010, 1, 1),
        });
        person.NameHistory.Add(new PersonNameHistory
        {
            Id = 2, LastName = "Garcia", FirstName = "Maria", ChangedOn = new DateOnly(2015, 5, 5),
        });

        var detail = PersonEndpoints.ToDetail.Compile().Invoke(person);

        Assert.Equal(
            new PersonIdentifierResponse(3, "MR", "A-100", "46D0523979", "CLIA"),
            Assert.Single(detail.Identifiers));
        Assert.Equal(
            [
                new PersonFormerNameResponse("Garcia", "Maria", null, new DateOnly(2015, 5, 5)),
                new PersonFormerNameResponse("Lopez", "Maria", null, new DateOnly(2010, 1, 1)),
            ],
            detail.FormerNames);
    }

    [Fact]
    public void ApplyRecordsTheNameItReplaces()
    {
        var person = Seeded();

        PersonEndpoints.Apply(person, Rewritten(), Today);

        var former = Assert.Single(person.NameHistory);
        Assert.Equal("Rodriguez", former.LastName);
        Assert.Equal("Maria", former.FirstName);
        Assert.Equal("Elena", former.MiddleName);
        Assert.Equal(Today, former.ChangedOn);
    }

    [Fact]
    public void ApplyRecordsNoNameWhereTheNameIsUnchanged()
    {
        var person = Seeded();

        PersonEndpoints.Apply(
            person, new UpdatePersonRequest(" Rodriguez ", "Maria", "Elena"), Today);

        Assert.Empty(person.NameHistory);
    }

    [Fact]
    public void ApplyNormalizesAnAddressAndTakesABlankCountyFromItsZip()
    {
        var person = Seeded();

        PersonEndpoints.Apply(person, new UpdatePersonRequest("Rodriguez")
        {
            Addresses =
            [
                new PersonAddressRequest(
                    Street: "9  ELM court", City: "ogden", State: "utah", PostalCode: "844011234"),
                new PersonAddressRequest(PostalCode: "84101", County: "UT", Ending: Today),
            ],
        }, Today, new Dictionary<string, string> { ["84401"] = "WB", ["84101"] = "SL" });

        var first = person.Addresses.First();
        Assert.Equal("9 Elm Court", first.Street);
        Assert.Equal("Ogden", first.City);
        Assert.Equal("UT", first.State);
        Assert.Equal("84401-1234", first.PostalCode);
        Assert.Equal("WB", first.County);
        Assert.Equal("UT", person.Addresses.Last().County);
    }

    [Fact]
    public void ApplyFillsAConnecticutAddressFromItsTownAndKeepsItsCountyEquivalent()
    {
        var person = Seeded();

        PersonEndpoints.Apply(person, new UpdatePersonRequest("Rodriguez")
        {
            Addresses =
            [
                new PersonAddressRequest(City: "new haven", State: "ct", PostalCode: "06510"),
                new PersonAddressRequest(
                    City: "Danbury", State: "CT", CountyEquivalent: "09190", Ending: Today),
            ],
        }, Today, new Dictionary<string, string> { ["06510"] = "09001" },
        [
            new CtTown { Town = "New Haven", County = "09009", PlanningRegion = "09170" },
            new CtTown { Town = "Danbury", County = "09001", PlanningRegion = "09190" },
        ]);

        var first = person.Addresses.First();
        Assert.Equal("09009", first.County);
        Assert.Equal("09170", first.CountyEquivalent);
        var last = person.Addresses.Last();
        Assert.Equal("09001", last.County);
        Assert.Equal("09190", last.CountyEquivalent);
    }

    /// <summary>The seeded person with their address geocoded by Census.</summary>
    private static Person Geocoded()
    {
        var person = Seeded();
        var address = person.Addresses.Single();
        address.Latitude = 40.760779m;
        address.Longitude = -111.891047m;
        address.GeocodedBy = PersonAddress.Census;
        return person;
    }

    [Fact]
    public void ApplyKeepsAHandTypedPairAsManual()
    {
        var person = Geocoded();

        PersonEndpoints.Apply(person, new UpdatePersonRequest("Rodriguez")
        {
            Addresses =
            [
                new PersonAddressRequest(Street: "1234 Main Street", Latitude: 40.7m, Longitude: -111.9m),
            ],
        }, Today);

        var address = person.Addresses.Single();
        Assert.Equal((40.7m, -111.9m, PersonAddress.Manual), (address.Latitude, address.Longitude, address.GeocodedBy));
    }

    [Fact]
    public void ApplyKeepsAStoredPairOnTheSameStreetWithItsSource()
    {
        var person = Geocoded();

        PersonEndpoints.Apply(person, new UpdatePersonRequest("Rodriguez")
        {
            Addresses =
            [
                new PersonAddressRequest(
                    Street: "1234 main street", Latitude: 40.760779m, Longitude: -111.891047m),
            ],
        }, Today);

        var address = person.Addresses.Single();
        Assert.Equal(
            (40.760779m, -111.891047m, PersonAddress.Census),
            (address.Latitude, address.Longitude, address.GeocodedBy));
    }

    [Fact]
    public void ApplyDropsAStoredPairCarriedOntoAChangedStreet()
    {
        var person = Geocoded();

        PersonEndpoints.Apply(person, new UpdatePersonRequest("Rodriguez")
        {
            Addresses =
            [
                new PersonAddressRequest(
                    Street: "9 Elm Court", Latitude: 40.760779m, Longitude: -111.891047m),
            ],
        }, Today);

        var address = person.Addresses.Single();
        Assert.Null(address.Latitude);
        Assert.Null(address.Longitude);
        Assert.Null(address.GeocodedBy);
    }

    [Fact]
    public void ApplyLeavesOneAddressCurrent()
    {
        var person = Seeded();

        PersonEndpoints.Apply(person, new UpdatePersonRequest("Rodriguez")
        {
            Addresses =
            [
                new PersonAddressRequest(Street: "1 Old Road", Beginning: new DateOnly(2010, 1, 1)),
                new PersonAddressRequest(Street: "2 New Road", Beginning: new DateOnly(2024, 3, 1)),
            ],
        }, Today);

        Assert.Equal(new DateOnly(2024, 3, 1), person.Addresses.First().Ending);
        Assert.Null(person.Addresses.Last().Ending);
    }

    [Fact]
    public void ApplyReplacesTheIdentifiers()
    {
        var person = Seeded();
        person.Identifiers.Add(new PersonIdentifier { Id = 3, Value = "OLD" });

        PersonEndpoints.Apply(person, new UpdatePersonRequest("Rodriguez")
        {
            Identifiers = [new PersonIdentifierRequest(" MR ", " A-100 ", "2.16.840.1", "oid")],
        }, Today);

        var identifier = Assert.Single(person.Identifiers);
        Assert.Equal(0, identifier.Id);
        Assert.Equal("MR", identifier.IdentifierType);
        Assert.Equal("A-100", identifier.Value);
        Assert.Equal("2.16.840.1", identifier.AssigningAuthority);
        Assert.Equal("OID", identifier.AssigningAuthorityType);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("CLIA", "46D0523979")]
    [InlineData("oid", "2.16.840.1")]
    public void WhyNotAPersonAcceptsAnIdentifierWithAValue(string? type, string? authority)
    {
        Assert.Null(PersonEndpoints.WhyNotAPerson(new UpdatePersonRequest("Rodriguez")
        {
            Identifiers = [new PersonIdentifierRequest("MR", "A-100", authority, type)],
        }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void WhyNotAPersonRefusesAnIdentifierWithNoValue(string? value)
    {
        Assert.Contains("identifiers[].value is required",
            PersonEndpoints.WhyNotAPerson(new UpdatePersonRequest("Rodriguez")
            {
                Identifiers = [new PersonIdentifierRequest("MR", value)],
            }),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAPersonRefusesAnAuthorityNamedByNeitherCliaNorOid()
    {
        Assert.Contains("must be CLIA or OID",
            PersonEndpoints.WhyNotAPerson(new UpdatePersonRequest("Rodriguez")
            {
                Identifiers = [new PersonIdentifierRequest("MR", "A-100", "Lab", "NPI")],
            }),
            StringComparison.Ordinal);
    }
}
