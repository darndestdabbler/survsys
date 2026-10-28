using Survsys.Api;
using Survsys.Contracts;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the contact-event route refuses before asking the database anything:
/// a contact named both ways or neither, an identifier nobody can have, and a
/// new person the person rule refuses.
/// </summary>
/// <remarks>
/// No database and no host: whether the exposure and the person are on file,
/// and what the route writes, is <c>Survsys.IntegrationTests</c>'s.
/// </remarks>
public class ContactEventEndpointsTests
{
    [Fact]
    public void WhyNotAContactEventAcceptsAPersonOnFile()
    {
        Assert.Null(ContactEventEndpoints.WhyNotAContactEvent(
            new CreateContactEventRequest(PersonId: 15, ContactType: "household")));
    }

    [Fact]
    public void WhyNotAContactEventAcceptsANewPerson()
    {
        Assert.Null(ContactEventEndpoints.WhyNotAContactEvent(
            new CreateContactEventRequest(NewPerson: new CreatePersonRequest("Ortiz", "Ana"))));
    }

    [Fact]
    public void WhyNotAContactEventRefusesAContactNamedBothWays()
    {
        Assert.Contains("exactly one way",
            ContactEventEndpoints.WhyNotAContactEvent(new CreateContactEventRequest(
                PersonId: 15, NewPerson: new CreatePersonRequest("Ortiz"))),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAContactEventRefusesAContactNamedNeitherWay()
    {
        Assert.Contains("exactly one way",
            ContactEventEndpoints.WhyNotAContactEvent(new CreateContactEventRequest()),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WhyNotAContactEventRefusesAnIdentifierNobodyCanHave(int personId)
    {
        Assert.Contains($"personId {personId} is not an identifier anyone has",
            ContactEventEndpoints.WhyNotAContactEvent(new CreateContactEventRequest(PersonId: personId)),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A new person is held to the rule every new person is: a last name.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void WhyNotAContactEventRefusesANewPersonWithNoLastName(string? lastName)
    {
        Assert.StartsWith("newPerson: ",
            ContactEventEndpoints.WhyNotAContactEvent(
                new CreateContactEventRequest(NewPerson: new CreatePersonRequest(lastName))),
            StringComparison.Ordinal);
    }
}
