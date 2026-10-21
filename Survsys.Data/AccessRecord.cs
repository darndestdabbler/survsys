namespace Survsys.Data;

/// <summary>
/// That a user opened a case morbidity report, and when: the legacy
/// <c>access_records</c>, the record of who viewed what.
/// </summary>
/// <remarks>
/// Written and never changed. A row a user or a report would take with it when
/// deleted would be an audit that forgets: the user is a foreign key that does
/// not cascade, and the report is a bare id that outlives the report.
/// </remarks>
public class AccessRecord
{
    public int Id { get; set; }

    /// <summary>The user who opened the report.</summary>
    public int UserId { get; set; }

    /// <summary>The report that was opened; null for an action not on one report.</summary>
    public int? PersonConditionId { get; set; }

    /// <summary>What the user did: <c>View</c>, <c>SignIn</c>, <c>Export</c> or <c>Search</c>.</summary>
    public string Action { get; set; } = "View";

    /// <summary>What the action was on, such as the view exported or the criteria searched.</summary>
    public string? Detail { get; set; }

    /// <summary>When it was opened, in UTC.</summary>
    public DateTime AccessedAt { get; set; }
}
