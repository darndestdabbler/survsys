namespace Survsys.Data;

/// <summary>
/// An outbreak: a named event a public health agency opens to group the cases
/// that belong to it, with the condition it is of and the agency that owns it.
/// </summary>
/// <remarks>
/// The columns are EpiTrax's own <c>outbreak</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, kept to the lean set this release opens an outbreak
/// with: its number and name, the date it is dated to, a description, the
/// condition and the owning agency, a status and when it was created. The
/// legacy's <c>created_user_id</c>, <c>parent_id</c>, <c>location_type</c>,
/// <c>investigator_id</c>, the report-approval columns and the epi-tracker
/// linkage are left to the sessions that need them, as the attachment and
/// form columns on other tables are.
///
/// <see cref="Name"/> is required where the legacy column is
/// <c>NOT NULL</c>, as <see cref="Agency.Name"/> is: an outbreak with no name
/// cannot be chosen from a list. The condition and the owning agency are real
/// foreign keys that do not cascade — a condition or an agency with outbreaks
/// against it cannot be deleted out from under them — and each is nullable,
/// because an outbreak can be opened before either is settled. The multi-agency
/// grants are <see cref="OutbreakAccess"/>, the report sections
/// <see cref="OutbreakReportSection"/> and the activity log
/// <see cref="OutbreakUpdate"/>; a case names its outbreak through
/// <see cref="PersonCondition.OutbreakId"/>.
/// </remarks>
public class Outbreak
{
    public int Id { get; set; }

    /// <summary>
    /// The outbreak's number, as the legacy <c>number</c> — a human-entered
    /// handle such as a year and a sequence; null until one is given.
    /// </summary>
    public string? Number { get; set; }

    /// <summary>The outbreak's name, as a dropdown and a list show it.</summary>
    public required string Name { get; set; }

    /// <summary>
    /// The day the outbreak is dated to, as the legacy <c>outbreak_date</c>;
    /// null until set. A <see cref="DateOnly"/> as the other dates on a case
    /// are, over a legacy <c>TIMESTAMP</c> that carries a day.
    /// </summary>
    public DateOnly? OutbreakDate { get; set; }

    /// <summary>
    /// What the outbreak is, as the legacy <c>description</c> — free text,
    /// left unbounded as <see cref="Note.Text"/> is, because it holds whatever
    /// somebody wrote.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The condition the outbreak is of, as the legacy <c>condition_id</c>;
    /// null until set. A real foreign key that does not cascade.
    /// </summary>
    public int? ConditionId { get; set; }

    /// <summary>
    /// The agency that owns the outbreak, as the legacy <c>agency_id</c>; null
    /// until set. A real foreign key that does not cascade. Other agencies see
    /// the outbreak through <see cref="OutbreakAccess"/>.
    /// </summary>
    public int? AgencyId { get; set; }

    /// <summary>
    /// The outbreak's status, as the legacy <c>status</c> int; 0 until moved.
    /// A plain coded int, as the legacy holds it, with no reference table
    /// behind it.
    /// </summary>
    public int Status { get; set; }

    /// <summary>When the outbreak was opened, as the legacy <c>created_at</c>.</summary>
    /// <remarks>
    /// A <see cref="DateTimeOffset"/> and not a <see cref="DateOnly"/>, as
    /// <see cref="PersonCondition.CreatedAt"/> is: this is the moment the
    /// outbreak was opened, and it carries its offset. Required, and set where
    /// the outbreak is created.
    /// </remarks>
    public DateTimeOffset CreatedAt { get; set; }
}
