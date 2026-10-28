namespace Survsys.Data;

/// <summary>
/// One message an export raised while its notification was built: the PHIN
/// variable it is about, what was said, and how severe it is (error, warning
/// or info). A detail of a <see cref="CdcExportLogEntry"/>, deleted with it.
/// </summary>
/// <remarks>
/// The legacy NMI <c>nmi.log_detail</c> row, as described in
/// <c>../nedss/docs/business-logic/NMI_EXPORT_PIPELINE.md</c>, with the
/// severity kept as its name rather than a <c>severity_id</c> into a lookup.
/// </remarks>
public class CdcExportLogDetail
{
    /// <summary>The detail's id. The key.</summary>
    public int Id { get; set; }

    /// <summary>The log row this detail belongs to, as a bare id into <c>nmi_log</c>.</summary>
    public int LogId { get; set; }

    /// <summary>
    /// The PHIN variable the message is about, or null where it is about the
    /// export as a whole. The legacy <c>phin_var</c>.
    /// </summary>
    public string? PhinVar { get; set; }

    /// <summary>What was said. Unbounded, as the message is as long as it is.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>The message's severity: <c>Error</c>, <c>Warning</c> or <c>Info</c>.</summary>
    public string Severity { get; set; } = string.Empty;
}
