namespace Survsys.Contracts;

/// <summary>
/// What an export by record number or MMWR range came to: how many cases were
/// queued for CDC notification, and which record numbers asked for are not on file.
/// </summary>
/// <param name="Queued">How many cases were queued.</param>
/// <param name="Unknown">The record numbers no case has; always empty for an MMWR range.</param>
public record NotificationExportResponse(int Queued, IReadOnlyList<string> Unknown);
