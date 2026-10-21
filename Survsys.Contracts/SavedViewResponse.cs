namespace Survsys.Contracts;

/// <summary>A saved view, as <c>/api/saved-views</c> returns one.</summary>
/// <param name="Id">The view's identifier.</param>
/// <param name="Name">The view's name.</param>
/// <param name="Columns">The columns it exports, in order, from <see cref="ExportColumns"/>.</param>
/// <param name="ConditionId">The condition it selects, or null for every condition.</param>
/// <param name="AgencyId">The investigating agency it selects, or null for every agency.</param>
/// <param name="WorkflowState">The workflow state code it selects, or null for every state.</param>
/// <param name="CaseStatus">The case status code it selects, or null for every status.</param>
/// <param name="OnsetFrom">The earliest event onset date it selects, or null.</param>
/// <param name="OnsetTo">The latest event onset date it selects, or null.</param>
public record SavedViewResponse(
    int Id,
    string Name,
    IReadOnlyList<string> Columns,
    int? ConditionId,
    int? AgencyId,
    string? WorkflowState,
    string? CaseStatus,
    DateOnly? OnsetFrom,
    DateOnly? OnsetTo);
