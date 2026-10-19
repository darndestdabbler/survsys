namespace Survsys.Contracts;

/// <summary>
/// One page of a collection, as the API tier returns it and the application
/// tier reads it. Every collection on the wire is paged, so every collection
/// on the wire comes back in this.
/// </summary>
/// <remarks>
/// It carries the page and nothing derived from it: the page number, the page
/// size and how many rows there are in total are the three facts a pager
/// cannot work out for itself, and anything else — the last page, whether
/// there is a next one — follows from them where it is needed.
///
/// This is the record that stands in for <c>Dabbler.Api.Querying</c>, which
/// the solution plan declined at this phase. If paging ever needs more than
/// this, that decision is worth revisiting; a dozen lines is not.
/// </remarks>
/// <param name="Items">The rows on this page, in the order the API returned them.</param>
/// <param name="Page">Which page this is, counting from one.</param>
/// <param name="PageSize">The largest number of rows a page of this request may hold.</param>
/// <param name="TotalCount">How many rows the whole collection holds, across every page.</param>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);
