namespace Survsys.Data;

/// <summary>
/// One section of an outbreak report: the fixed headings an outbreak's report
/// is written under, from Context/Background through Key Investigators.
/// </summary>
/// <remarks>
/// EpiTrax's own <c>report_section</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c> and seeded by
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0003__init.sql</c>:
/// an id and a description. The seven sections are a fixed reference set, not
/// an installation-configurable list, so the configuration seeds them with
/// <see cref="AgencyConfiguration"/>'s <c>HasData</c>. An outbreak's text for a
/// section is <see cref="OutbreakReportSection"/>.
/// </remarks>
public class ReportSection
{
    public int Id { get; set; }

    /// <summary>The section's heading, as a report editor shows it.</summary>
    public required string Description { get; set; }
}
