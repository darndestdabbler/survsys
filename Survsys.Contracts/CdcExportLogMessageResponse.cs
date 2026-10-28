namespace Survsys.Contracts;

/// <summary>
/// One message an export raised while its notification was built, as the
/// export-log detail returns it: the legacy <c>nmi.log_detail</c> row.
/// </summary>
/// <param name="PhinVar">The PHIN variable the message is about, or null where it is about the export as a whole.</param>
/// <param name="Message">What was said.</param>
/// <param name="Severity">The message's severity: <c>Error</c>, <c>Warning</c> or <c>Info</c>.</param>
public record CdcExportLogMessageResponse(string? PhinVar, string Message, string Severity);
