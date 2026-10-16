namespace Survsys.Data;

/// <summary>
/// A disease or condition a case can be reported for: the reference table
/// every case morbidity report points at, and the thinnest real table in the
/// legacy catalogue that a screen can show.
/// </summary>
/// <remarks>
/// The columns are EpiTrax's own <c>condition</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, down to their snake_case names — so a later
/// migration of the legacy data reads straight across rather than through a
/// column-name translation nobody kept.
///
/// Two columns are tightened against what the legacy schema permits, because
/// the legacy data never used the latitude: <see cref="Name"/> is required
/// where <c>name VARCHAR</c> was nullable, and <see cref="Active"/> is a
/// <c>bool</c> where <c>active BOOL</c> was nullable. All 186 seeded rows
/// carry a name and a true-or-false active flag; a condition with neither
/// cannot be shown or chosen, so the schema says so.
///
/// There is no behaviour here and no navigation out.
/// <see cref="ConditionGroupId"/> stays a plain nullable int: the
/// <c>condition_group</c> table it points at is a later session's, and a
/// navigation to a table that does not exist is not a thing to write early.
/// </remarks>
public class Condition
{
    public int Id { get; set; }

    /// <summary>The condition's name, as a list screen shows it.</summary>
    public required string Name { get; set; }

    /// <summary>
    /// Prose shown above the Contacts tab, instructing the investigator how
    /// contacts are identified for this condition. Long, and per-condition.
    /// </summary>
    public string? ContactLeadIn { get; set; }

    /// <summary>The same, for exposure places.</summary>
    public string? PlaceLeadIn { get; set; }

    /// <summary>The same, for treatments.</summary>
    public string? TreatmentLeadIn { get; set; }

    /// <summary>Whether the condition may be chosen for a new report.</summary>
    public bool Active { get; set; }

    /// <summary>
    /// The CDC's code for the condition, five digits where the condition has
    /// one; null where it is not nationally notifiable.
    /// </summary>
    public string? CdcCode { get; set; }

    /// <summary>
    /// The <c>condition_group</c> this condition belongs to, if any. A bare
    /// identifier rather than a navigation, until the table exists.
    /// </summary>
    public int? ConditionGroupId { get; set; }
}
