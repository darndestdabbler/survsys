namespace Survsys.Contracts;

/// <summary>
/// A code to add to a list or to save over one.
/// </summary>
/// <param name="Code">The code; required, and unique within its list.</param>
/// <param name="Description">The text a picker shows the code by, or null.</param>
/// <param name="SortOrder">Where the code falls in its list.</param>
/// <param name="Active">False to retire the code: kept for the records naming it, no longer offered.</param>
/// <param name="ConditionIds">
/// For a test type, the conditions it is offered for, replacing the set held;
/// null or empty for none. Refused on any other list.
/// </param>
public record SaveCodeRequest(
    string? Code,
    string? Description = null,
    int SortOrder = 0,
    bool Active = true,
    IReadOnlyList<int>? ConditionIds = null);
