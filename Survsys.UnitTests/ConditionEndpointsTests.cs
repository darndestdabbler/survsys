using System.Linq.Expressions;
using Survsys.Api;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The two pure parts of the conditions endpoint: which pages it will return,
/// and what the stored row becomes on the wire. Both without a database and
/// without a host — what needs either is the integration suite's.
/// </summary>
public class ConditionEndpointsTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, ConditionEndpoints.DefaultPageSize)]
    [InlineData(1, ConditionEndpoints.MaxPageSize)]
    [InlineData(7, 25)]
    [InlineData(int.MaxValue, 1)]
    public void WhyNotAPageAcceptsAPageWithinItsBounds(int page, int pageSize)
    {
        Assert.Null(ConditionEndpoints.WhyNotAPage(page, pageSize));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(int.MinValue, 10)]
    public void WhyNotAPageRefusesAPageBelowOne(int page, int pageSize)
    {
        Assert.Contains("page must be 1 or greater",
            ConditionEndpoints.WhyNotAPage(page, pageSize), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void WhyNotAPageRefusesAPageSizeBelowOne(int page, int pageSize)
    {
        Assert.Contains("pageSize must be 1 or greater",
            ConditionEndpoints.WhyNotAPage(page, pageSize), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ConditionEndpoints.MaxPageSize + 1)]
    [InlineData(int.MaxValue)]
    public void WhyNotAPageRefusesAPageSizeOverTheMaximum(int pageSize)
    {
        Assert.Contains($"pageSize must be {ConditionEndpoints.MaxPageSize} or fewer",
            ConditionEndpoints.WhyNotAPage(1, pageSize), StringComparison.Ordinal);
    }

    /// <summary>
    /// The bound that is easy to leave out. EF Core's <c>Skip</c> takes an
    /// <c>int</c>, so a page this far out would otherwise overflow into a
    /// negative skip and reach SQL Server as something other than a page.
    /// </summary>
    [Fact]
    public void WhyNotAPageRefusesAPagePastTheLastAddressableRow()
    {
        Assert.Contains("is past the last row",
            ConditionEndpoints.WhyNotAPage(int.MaxValue, 2), StringComparison.Ordinal);
    }

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
