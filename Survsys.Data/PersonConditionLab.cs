namespace Survsys.Data;

/// <summary>
/// The link between a case morbidity report and a laboratory specimen: which
/// specimens belong to which report.
/// </summary>
/// <remarks>
/// The columns are EpiTrax's own <c>person_condition_lab</c> table, as declared
/// in <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, down to their snake_case names — the two foreign keys
/// and nothing else, so the row is the link and carries no attributes of its
/// own. <c>../nedss/docs/db-snapshots/session-011/02-after-lab-save.txt</c>
/// dumps one such row, linking specimen 11 to report 14.
///
/// The link lives here rather than on <see cref="Lab"/> because the legacy
/// data puts it here: <c>lab.person_condition_id</c> is left null and this
/// join carries the relationship. There is no identity column and no
/// navigation out; the pair of ids is the key, and the session that walks from
/// a report to its specimens adds the navigation it needs.
/// </remarks>
public class PersonConditionLab
{
    /// <summary>The report a specimen is linked to.</summary>
    public int PersonConditionId { get; set; }

    /// <summary>The specimen linked to the report.</summary>
    public int LabId { get; set; }
}
