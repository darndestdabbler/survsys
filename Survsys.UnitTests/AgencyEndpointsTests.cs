using Survsys.Api;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The agency list the creation screen's Investigating Agency dropdown is
/// filled from, and the lookup the creation endpoint asks of it.
/// </summary>
/// <remarks>
/// A stand-in for the legacy <c>agency</c> table until the session that writes
/// that table — <see cref="AgencyEndpoints"/> says so of itself at length.
/// These are the properties a dropdown and a foreign key need of it whatever
/// it is standing in for: distinct identifiers, names that can be shown, and
/// one order.
/// </remarks>
public class AgencyEndpointsTests
{
    /// <summary>
    /// The names <c>../nedss/docs/functional/epitrax-cmr-workflow.md</c>
    /// records from the legacy creation screen, written out here rather than
    /// read off the list under test: a test that took the expected order from
    /// the thing it is testing would pass whatever order that was in.
    /// </summary>
    private static readonly string[] Legacy =
    [
        "Bear River",
        "Central Utah",
        "Davis County",
        "Out of State",
        "Salt Lake County",
        "San Juan County",
        "Southeastern Utah",
        "Southwest Utah",
        "Summit County",
        "Tooele County",
        "TriCounty",
        "Utah County",
        "Utah Navajo",
        "Utah State",
        "Wasatch County",
        "Weber-Morgan",
    ];

    [Fact]
    public void TheListIsTheAgenciesTheLegacyScreenOffered()
    {
        Assert.Equal(Legacy, AgencyEndpoints.All.Select(agency => agency.Name));
    }

    /// <summary>
    /// In name order, which is the order a dropdown shows them in. Asserted
    /// against a sort of the names rather than against the literal list, so
    /// that an agency added out of place fails here rather than in front of a
    /// user.
    /// </summary>
    [Fact]
    public void TheAgenciesAreInNameOrder()
    {
        var names = AgencyEndpoints.All.Select(agency => agency.Name).ToList();

        Assert.Equal(names.OrderBy(name => name, StringComparer.Ordinal), names);
    }

    /// <summary>
    /// What a foreign key needs of them: an identifier that is one agency's
    /// and nobody else's. A duplicate here would be a report assigned to two
    /// agencies at once, or to neither.
    /// </summary>
    [Fact]
    public void EveryIdentifierIsDistinct()
    {
        var ids = AgencyEndpoints.All.Select(agency => agency.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void EveryAgencyHasANameAndAnIdentifierThatCanBeStored()
    {
        Assert.NotEmpty(AgencyEndpoints.All);

        foreach (var agency in AgencyEndpoints.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(agency.Name));
            Assert.True(agency.Id > 0);
        }
    }

    [Fact]
    public void FindAnswersTheAgencyWithThatIdentifier()
    {
        foreach (var agency in AgencyEndpoints.All)
        {
            Assert.Equal(agency, AgencyEndpoints.Find(agency.Id));
        }
    }

    /// <summary>
    /// The answer the creation endpoint turns into a 400: an agency nobody has
    /// is nothing, rather than an exception or the first agency in the list.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(99)]
    [InlineData(int.MaxValue)]
    public void FindAnswersNothingForAnIdentifierNoAgencyHas(int id)
    {
        Assert.Null(AgencyEndpoints.Find(id));
    }
}
