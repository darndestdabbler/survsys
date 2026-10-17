namespace Survsys.Api;

/// <summary>
/// Which pages of a collection this tier will return, and why a page it will
/// not return is not one.
/// </summary>
/// <remarks>
/// Session 5 wrote this rule inside <see cref="ConditionEndpoints"/>, where it
/// was the only paged endpoint there was. It is here now because the persons
/// endpoints need the same rule, and a second copy of a bound is a second
/// bound: the two would agree until one of them was changed.
///
/// Pure, and public because the unit suite proves it without starting a host.
/// Every collection on the wire comes back in
/// <c>Survsys.Contracts.PagedResult{T}</c>, and every endpoint that returns
/// one asks this first.
/// </remarks>
public static class Paging
{
    /// <summary>The page returned when a caller asks for no particular one.</summary>
    public const int DefaultPage = 1;

    /// <summary>The page size used when a caller asks for no particular one.</summary>
    public const int DefaultPageSize = 50;

    /// <summary>
    /// The largest page this tier will return. A caller that wants more rows
    /// asks for another page; it may not ask for an unbounded one.
    /// </summary>
    public const int MaxPageSize = 200;

    /// <summary>
    /// Why the requested page is not a page this tier can return, or null when
    /// it is.
    /// </summary>
    /// <remarks>
    /// The last clause is the one that is easy to forget: <c>Skip</c> takes an
    /// <c>int</c>, so a page number large enough to overflow
    /// <c>(page - 1) * pageSize</c> would otherwise reach the query as a
    /// negative skip rather than as a bad request.
    /// </remarks>
    public static string? WhyNotAPage(int page, int pageSize)
    {
        if (page < 1)
        {
            return $"page must be 1 or greater, and {page} was asked for.";
        }

        if (pageSize < 1)
        {
            return $"pageSize must be 1 or greater, and {pageSize} was asked for.";
        }

        if (pageSize > MaxPageSize)
        {
            return $"pageSize must be {MaxPageSize} or fewer, and {pageSize} was asked for.";
        }

        if ((long)(page - 1) * pageSize > int.MaxValue)
        {
            return $"page {page} at a pageSize of {pageSize} is past the last row this "
                + "endpoint can address.";
        }

        return null;
    }
}
