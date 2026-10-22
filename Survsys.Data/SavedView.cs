namespace Survsys.Data;

/// <summary>
/// A user's saved view of the case morbidity reports: which columns to export
/// and which reports to select.
/// </summary>
/// <remarks>
/// A view belongs to the user who saved it and nobody else reads it. Every
/// filter left null selects every report, so a view with no filters exports
/// them all.
/// </remarks>
public class SavedView
{
    public int Id { get; set; }

    /// <summary>The user who saved the view.</summary>
    public int UserId { get; set; }

    /// <summary>The view's name; unique among its user's views.</summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// The chosen columns, in order, as names from the export column catalogue
    /// separated by commas.
    /// </summary>
    public string Columns { get; set; } = "";

    /// <summary>Only reports of this condition.</summary>
    public int? ConditionId { get; set; }

    /// <summary>Only reports this agency investigates.</summary>
    public int? AgencyId { get; set; }

    /// <summary>Only reports in this workflow state, by code.</summary>
    public string? WorkflowState { get; set; }

    /// <summary>Only reports with this case status, by code.</summary>
    public string? CaseStatus { get; set; }

    /// <summary>Only reports whose event onset date is on or after this.</summary>
    public DateOnly? OnsetFrom { get; set; }

    /// <summary>Only reports whose event onset date is on or before this.</summary>
    public DateOnly? OnsetTo { get; set; }
}
