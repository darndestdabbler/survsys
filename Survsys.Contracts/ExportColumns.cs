namespace Survsys.Contracts;

/// <summary>
/// The columns a saved view may export: a fixed catalogue, because each one
/// is a field the API tier knows how to read off a case morbidity report.
/// </summary>
/// <remarks>
/// Here rather than in the API tier because both tiers read them: the API
/// tier writes the CSV, and the application tier offers the choice. The names
/// are what a view's columns are stored as, what travel on the wire and what
/// head the exported file's columns, so a name is never changed once it is in
/// use.
/// </remarks>
public static class ExportColumns
{
    /// <summary>The report's record number.</summary>
    public const string RecordNumber = "record_number";

    /// <summary>The person's last name.</summary>
    public const string LastName = "last_name";

    /// <summary>The person's first name.</summary>
    public const string FirstName = "first_name";

    /// <summary>The person's date of birth.</summary>
    public const string BirthDate = "birth_date";

    /// <summary>The condition's name.</summary>
    public const string Condition = "condition";

    /// <summary>The investigating agency's name.</summary>
    public const string Agency = "agency";

    /// <summary>The report's workflow state code.</summary>
    public const string WorkflowState = "workflow_state";

    /// <summary>The report's case status code.</summary>
    public const string CaseStatus = "case_status";

    /// <summary>The event onset date.</summary>
    public const string EventOnsetDate = "event_onset_date";

    /// <summary>The date the condition was first reported to public health.</summary>
    public const string FirstReportedPhDate = "first_reported_ph_date";

    /// <summary>The MMWR week.</summary>
    public const string MmwrWeek = "mmwr_week";

    /// <summary>The MMWR year.</summary>
    public const string MmwrYear = "mmwr_year";

    /// <summary>When the report was created.</summary>
    public const string CreatedAt = "created_at";

    /// <summary>Every column there is, in the order a screen lists them.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        RecordNumber, LastName, FirstName, BirthDate, Condition, Agency, WorkflowState,
        CaseStatus, EventOnsetDate, FirstReportedPhDate, MmwrWeek, MmwrYear, CreatedAt,
    ];
}
