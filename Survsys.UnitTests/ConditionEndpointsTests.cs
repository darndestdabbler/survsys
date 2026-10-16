using System.Linq.Expressions;
using Survsys.Api;
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
}
