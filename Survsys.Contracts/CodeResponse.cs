namespace Survsys.Contracts;

/// <summary>
/// One code on a reference code list.
/// </summary>
/// <param name="Id">The code's identifier, which a coded field holding an integer stores.</param>
/// <param name="List">The list the code is on, one of <see cref="CodeLists.All"/>.</param>
/// <param name="Code">The code, which a coded field holding a string stores.</param>
/// <param name="Description">The text a picker shows the code by, or null where the code is its own label.</param>
/// <param name="SortOrder">Where the code falls in its list.</param>
/// <param name="Active">False once the code is retired and no longer offered.</param>
/// <param name="ConditionIds">The conditions a test type is offered for; empty on every other list.</param>
public record CodeResponse(
    int Id,
    string List,
    string Code,
    string? Description,
    int SortOrder,
    bool Active,
    IReadOnlyList<int> ConditionIds);
