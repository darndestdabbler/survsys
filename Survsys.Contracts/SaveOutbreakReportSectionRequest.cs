namespace Survsys.Contracts;

/// <summary>
/// The text to write under one section of an outbreak's report: what
/// <c>PUT /api/outbreaks/{id}/report/{sectionId}</c> takes.
/// </summary>
/// <remarks>
/// A save over the one section: the text replaces whatever was written under
/// it, and a null or blank text clears the section while keeping its author
/// and the fact that it was touched. The author is the user writing the
/// section, which the endpoint stamps onto the row.
/// </remarks>
/// <param name="UserId">The user writing the section.</param>
/// <param name="ReportText">What to write under the section, or null to clear it.</param>
public record SaveOutbreakReportSectionRequest(int UserId, string? ReportText = null);
