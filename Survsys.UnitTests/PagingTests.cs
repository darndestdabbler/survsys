using Survsys.Api;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// Which pages the API tier will return. The rule session 5 wrote inside
/// <c>ConditionEndpoints</c>, now the one every paged endpoint asks, and these
/// are its tests — moved here with it, because a public method takes its tests
/// where it goes.
/// </summary>
public class PagingTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, Paging.DefaultPageSize)]
    [InlineData(1, Paging.MaxPageSize)]
    [InlineData(7, 25)]
    [InlineData(int.MaxValue, 1)]
    public void WhyNotAPageAcceptsAPageWithinItsBounds(int page, int pageSize)
    {
        Assert.Null(Paging.WhyNotAPage(page, pageSize));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(int.MinValue, 10)]
    public void WhyNotAPageRefusesAPageBelowOne(int page, int pageSize)
    {
        Assert.Contains("page must be 1 or greater",
            Paging.WhyNotAPage(page, pageSize), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void WhyNotAPageRefusesAPageSizeBelowOne(int page, int pageSize)
    {
        Assert.Contains("pageSize must be 1 or greater",
            Paging.WhyNotAPage(page, pageSize), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Paging.MaxPageSize + 1)]
    [InlineData(int.MaxValue)]
    public void WhyNotAPageRefusesAPageSizeOverTheMaximum(int pageSize)
    {
        Assert.Contains($"pageSize must be {Paging.MaxPageSize} or fewer",
            Paging.WhyNotAPage(1, pageSize), StringComparison.Ordinal);
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
            Paging.WhyNotAPage(int.MaxValue, 2), StringComparison.Ordinal);
    }
}
