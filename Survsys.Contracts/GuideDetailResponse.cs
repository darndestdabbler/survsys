namespace Survsys.Contracts;

/// <summary>
/// A notification guide whole: its own fields, the PHIN variables it carries,
/// the guides it links, and its header variables.
/// </summary>
/// <param name="Id">The guide's id.</param>
/// <param name="Name">The guide's name.</param>
/// <param name="Version">The guide's version, or null.</param>
/// <param name="PhinVars">The PHIN variables it carries, by identifier, in order.</param>
/// <param name="LinkedGuideIds">The guides whose variables it carries alongside its own.</param>
/// <param name="Variables">Each header variable, with the guide's own value where it sets one.</param>
public record GuideDetailResponse(
    int Id,
    string Name,
    string? Version,
    IReadOnlyList<string> PhinVars,
    IReadOnlyList<int> LinkedGuideIds,
    IReadOnlyList<GuideVariableRow> Variables);
