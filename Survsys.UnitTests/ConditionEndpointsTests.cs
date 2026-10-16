using System.Linq.Expressions;
using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the conditions endpoint makes of a stored row, without a database and
/// without a host — what needs either is the integration suite's.
/// </summary>
/// <remarks>
/// The page bounds this endpoint answers by are no longer its own: they are
/// <c>Paging</c>'s, which every paged endpoint asks, and
/// <see cref="PagingTests"/> proves them there.
/// </remarks>
public class ConditionEndpointsTests
{
    [Fact]
    public void ToResponseCarriesTheFourWireFields()
    {
        var response = ConditionEndpoints.ToResponse.Compile().Invoke(new Condition
        {
            Id = 74,
            Name = "Hepatitis A",
            CdcCode = "10110",
            Active = true,
            ContactLeadIn = "Obtain information for all contacts exposed during the communicable period.",
            PlaceLeadIn = "Identify every place the case ate or worked.",
            TreatmentLeadIn = "Immune globulin within two weeks of exposure.",
            ConditionGroupId = 3,
        });

        Assert.Equal(new(74, "Hepatitis A", "10110", true), response);
    }

    [Fact]
    public void ToResponseCarriesAConditionWithNoCdcCode()
    {
        var response = ConditionEndpoints.ToResponse.Compile().Invoke(new Condition
        {
            Id = 9,
            Name = "Contact",
            CdcCode = null,
            Active = false,
        });

        Assert.Equal(new(9, "Contact", null, false), response);
    }

    /// <summary>
    /// The projection reads four columns and no others, which is what keeps
    /// the three lead-in columns — paragraphs of prose apiece — out of the
    /// <c>SELECT</c> list EF Core builds from it. Read off the expression
    /// tree, because that is the thing EF Core translates.
    /// </summary>
    [Fact]
    public void ToResponseReadsNoColumnItDoesNotPutOnTheWire()
    {
        var construction = Assert.IsAssignableFrom<NewExpression>(ConditionEndpoints.ToResponse.Body);

        var read = construction.Arguments
            .Cast<MemberExpression>()
            .Select(argument => argument.Member.Name)
            .ToList();

        Assert.Equal(
            [nameof(Condition.Id), nameof(Condition.Name), nameof(Condition.CdcCode), nameof(Condition.Active)],
            read);
    }

    private static Condition HepatitisA() => new()
    {
        Id = 74,
        Name = "Hepatitis A",
        CdcCode = "10110",
        Active = true,
        ContactLeadIn = "Obtain information for all contacts.",
        PlaceLeadIn = "Identify every place the case ate or worked.",
        TreatmentLeadIn = "Immune globulin within two weeks of exposure.",
        ConditionGroupId = 3,
    };

    private static readonly ConditionDetailResponse HepatitisADetail = new(
        74,
        "Hepatitis A",
        "10110",
        true,
        "Obtain information for all contacts.",
        "Identify every place the case ate or worked.",
        "Immune globulin within two weeks of exposure.");

    [Fact]
    public void ToDetailCarriesEveryColumnButTheGroup()
    {
        Assert.Equal(HepatitisADetail, ConditionEndpoints.ToDetail.Compile().Invoke(HepatitisA()));
    }

    [Fact]
    public void DetailIsTheReadMappingItself()
    {
        Assert.Equal(HepatitisADetail, ConditionEndpoints.Detail(HepatitisA()));
    }

    [Fact]
    public void WhyNotAConditionAcceptsANameAndEveryOptionalField()
    {
        Assert.Null(ConditionEndpoints.WhyNotACondition(
            new SaveConditionRequest("Hepatitis A", true, "10110", "a", "b", "c")));
    }

    [Fact]
    public void WhyNotAConditionAcceptsAConditionWithOnlyAName()
    {
        Assert.Null(ConditionEndpoints.WhyNotACondition(new SaveConditionRequest("Contact", false)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void WhyNotAConditionRefusesAConditionWithNoName(string name)
    {
        Assert.Contains("name is required",
            ConditionEndpoints.WhyNotACondition(new SaveConditionRequest(name, true)),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAConditionRefusesANameLongerThanTheColumn()
    {
        Assert.Contains("name is longer than 200",
            ConditionEndpoints.WhyNotACondition(new SaveConditionRequest(new string('a', 201), true)),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAConditionRefusesACdcCodeLongerThanTheColumn()
    {
        Assert.Contains("cdcCode is longer than 20",
            ConditionEndpoints.WhyNotACondition(new SaveConditionRequest("Hepatitis A", true, new string('1', 21))),
            StringComparison.Ordinal);
    }
}
