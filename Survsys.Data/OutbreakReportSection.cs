namespace Survsys.Data;

/// <summary>
/// The text of one section of one outbreak's report: what somebody wrote under
/// a <see cref="ReportSection"/> heading for a given outbreak.
/// </summary>
/// <remarks>
/// EpiTrax's own <c>outbreak_report_section</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>: the outbreak, the report section, the author and the
/// text. A section is written once per outbreak — the configuration keys the
/// pair — so writing a section again is saving over the one row, not adding
/// another.
/// </remarks>
public class OutbreakReportSection
{
    public int Id { get; set; }

    /// <summary>The outbreak the text belongs to.</summary>
    public int OutbreakId { get; set; }

    /// <summary>The report section the text is written under.</summary>
    public int ReportSectionId { get; set; }

    /// <summary>Who wrote the section, as the legacy <c>user_id</c>.</summary>
    public int UserId { get; set; }

    /// <summary>
    /// What was written, as the legacy <c>report_text</c> — free text, left
    /// unbounded as <see cref="Note.Text"/> is, because it holds a section of
    /// a report; null until written.
    /// </summary>
    public string? ReportText { get; set; }
}
