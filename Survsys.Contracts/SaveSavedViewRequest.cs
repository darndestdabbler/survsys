namespace Survsys.Contracts;

/// <summary>
/// A saved view to create or to save over an existing one: what
/// <c>POST /api/saved-views</c> and <c>PUT /api/saved-views/{id}</c> take.
/// </summary>
/// <remarks>
/// A save replaces the whole view. Every filter left null selects every
/// report.
/// </remarks>
/// <param name="Name">The view's name. Required, and unique among the caller's views.</param>
/// <param name="Columns">The columns to export, in order, each a name from <see cref="ExportColumns"/>.</param>
/// <param name="ConditionId">Only reports of this condition.</param>
/// <param name="AgencyId">Only reports this agency investigates.</param>
/// <param name="WorkflowState">Only reports in this workflow state, by code.</param>
/// <param name="CaseStatus">Only reports with this case status, by code.</param>
/// <param name="OnsetFrom">Only reports whose event onset date is on or after this.</param>
/// <param name="OnsetTo">Only reports whose event onset date is on or before this.</param>
public record SaveSavedViewRequest(
    string Name,
    IReadOnlyList<string>? Columns,
    int? ConditionId = null,
    int? AgencyId = null,
    string? WorkflowState = null,
    string? CaseStatus = null,
    DateOnly? OnsetFrom = null,
    DateOnly? OnsetTo = null);
