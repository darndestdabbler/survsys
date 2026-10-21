namespace Survsys.Contracts;

/// <summary>
/// The cases to export to CDC by record number: what
/// <c>POST /api/notification-queue/records</c> takes, queueing each case it knows.
/// </summary>
/// <param name="RecordNumbers">The cases' record numbers; blanks and repeats are ignored.</param>
public record ExportRecordsRequest(IReadOnlyList<string> RecordNumbers);
