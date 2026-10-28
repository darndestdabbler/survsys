namespace Survsys.Contracts;

/// <summary>
/// What classifying a case on save decided, as a route that saved one answers
/// it in its <c>Case-Classification</c> header.
/// </summary>
/// <param name="ReportId">The case that was classified.</param>
/// <param name="CaseStatus">The case's status after it, as its code, or null where it has none.</param>
/// <param name="Findings">One line per rule that had something to say.</param>
public record CaseClassificationResponse(int ReportId, string? CaseStatus, IReadOnlyList<string> Findings);
