namespace Survsys.Contracts;

/// <summary>
/// One section of one outbreak's report, as the report screen shows it: the
/// section, its heading, and the text written under it.
/// </summary>
/// <remarks>
/// The report comes back as every section, written or not, so the editor shows
/// the whole report and not only the sections somebody has reached: a section
/// nobody has written has a null <see cref="ReportText"/> rather than no row.
/// </remarks>
/// <param name="ReportSectionId">The section the text is written under.</param>
/// <param name="Section">The section's heading, as the editor shows it.</param>
/// <param name="ReportText">What was written under the section, or null where unwritten.</param>
public record OutbreakReportSectionResponse(
    int ReportSectionId,
    string Section,
    string? ReportText);
