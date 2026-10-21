using Survsys.Api;
using Survsys.Contracts;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The person a batch import makes of an accepted row, with no database: the
/// names, birth date and sex carried across, and an address, a telephone and
/// an email only where the row gives one.
/// </summary>
/// <remarks>
/// What the import writes, and what it refuses, is
/// <c>Survsys.IntegrationTests</c>'s, against a real SQL Server; the parsing
/// is <see cref="ContactImportParserTests"/>'s.
/// </remarks>
public class ContactImportEndpointsTests
{
    [Fact]
    public void ToPersonCarriesEveryFieldOfTheRow()
    {
        var person = ContactImportEndpoints.ToPerson(new ImportedContact(
            2, "Ana", "Lucia", "Ortiz", new DateOnly(1990, 4, 2), "F",
            "12 Elm St", "Apt 3", "Hartford", "CT", "06103", "HA",
            "860-555-0100", "ana@example.org"));

        Assert.Equal("Ortiz", person.LastName);
        Assert.Equal("Ana", person.FirstName);
        Assert.Equal("Lucia", person.MiddleName);
        Assert.Equal(new DateOnly(1990, 4, 2), person.BirthDate);
        Assert.Equal("F", person.BirthGender);

        var address = Assert.Single(person.Addresses);
        Assert.Equal("12 Elm St", address.Street);
        Assert.Equal("Apt 3", address.UnitNumber);
        Assert.Equal("Hartford", address.City);
        Assert.Equal("CT", address.State);
        Assert.Equal("06103", address.PostalCode);
        Assert.Equal("HA", address.County);

        Assert.Equal("860-555-0100", Assert.Single(person.Telephones).PhoneNumber);
        Assert.Equal("ana@example.org", Assert.Single(person.Emails).EmailAddress);
    }

    /// <summary>
    /// A row with only the required fields makes a person with no address,
    /// telephone or email rows, rather than empty ones.
    /// </summary>
    [Fact]
    public void ToPersonAddsNoChildRowsTheRowDoesNotGive()
    {
        var person = ContactImportEndpoints.ToPerson(new ImportedContact(
            2, null, null, "Ortiz", new DateOnly(1990, 4, 2), null,
            null, null, null, null, null, null, null, null));

        Assert.Null(person.FirstName);
        Assert.Null(person.BirthGender);
        Assert.Empty(person.Addresses);
        Assert.Empty(person.Telephones);
        Assert.Empty(person.Emails);
    }

    /// <summary>One part of an address is enough to record the address.</summary>
    [Fact]
    public void ToPersonRecordsAnAddressFromAnyOneOfItsParts()
    {
        var person = ContactImportEndpoints.ToPerson(new ImportedContact(
            2, null, null, "Ortiz", new DateOnly(1990, 4, 2), null,
            null, null, "Hartford", null, null, null, null, null));

        Assert.Equal("Hartford", Assert.Single(person.Addresses).City);
    }
}
