namespace Survsys.Contracts;

/// <summary>
/// What an ad-hoc case-count report may group by: a fixed catalogue, because
/// each one is a field the API tier knows how to group a case morbidity report
/// on.
/// </summary>
/// <remarks>
/// Here rather than in the API tier because both tiers read them, as
/// <see cref="ExportColumns"/> are: the API tier groups by them, and the
/// application tier offers the choice. The names travel on the wire, so a name
/// is never changed once it is in use.
/// </remarks>
public static class ReportGroupings
{
    /// <summary>By the condition's name.</summary>
    public const string Condition = "condition";

    /// <summary>By the investigating agency's name.</summary>
    public const string Agency = "agency";

    /// <summary>By the case status code.</summary>
    public const string CaseStatus = "case_status";

    /// <summary>By the workflow state code.</summary>
    public const string WorkflowState = "workflow_state";

    /// <summary>By the MMWR year the case is counted in.</summary>
    public const string MmwrYear = "mmwr_year";

    /// <summary>Every grouping there is, in the order a screen offers them.</summary>
    public static IReadOnlyList<string> All { get; } =
        [Condition, Agency, CaseStatus, WorkflowState, MmwrYear];
}
