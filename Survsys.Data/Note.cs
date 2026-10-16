namespace Survsys.Data;

/// <summary>
/// A note: one thing somebody wrote about a case morbidity report — clinical
/// or administrative — with who wrote it and when.
/// </summary>
/// <remarks>
/// The columns are EpiTrax's own <c>note</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c> and dumped in
/// <c>../nedss/docs/db-snapshots/session-012/05-after-admin-fill.txt</c>, down
/// to their snake_case names — so a later migration of the legacy data reads
/// straight across rather than through a column-name translation nobody kept.
/// The screen is the Notes tab's Notes section, as
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps it: a note type of Clinical or Administrative, the text, and a list
/// showing each note's creator, type, created date and a strikethrough
/// checkbox.
///
/// <strong>Strikethrough is the soft delete.</strong> A note is never updated
/// and never deleted: it is struck through, stays on the list and is shown
/// struck, which is what the legacy checkbox does and what this release's
/// Notes tab offers. <see cref="Struckthrough"/> is therefore the only column
/// anything changes after a note is recorded.
///
/// The report, the exposure, the facility and the user are bare nullable ids
/// and not navigations, exactly as <see cref="Exposure.PersonConditionId"/>
/// and <see cref="Lab.SpecimenSourceId"/> are: there is no <c>user</c> table
/// and no <c>facility</c> table in this schema to point at, and the exposure a
/// note may hang off is read by composing a query rather than by walking a
/// navigation the entities do not have. There is no behaviour here and no
/// navigation out.
/// </remarks>
public class Note
{
    public int Id { get; set; }

    /// <summary>
    /// What was written, as the legacy <c>note</c> column — free text, left
    /// unbounded, as <see cref="PersonAddress.Note"/> is, because it holds
    /// whatever somebody wrote.
    /// </summary>
    /// <remarks>
    /// <c>Text</c> and not <c>Note</c>: a property may not carry the name of
    /// the type that declares it. The column keeps the legacy name.
    ///
    /// Plain text. The legacy editor is a rich text editor and the legacy
    /// column holds its HTML; this release stores what a text area gave it,
    /// which round-trips through the same column without this tier having to
    /// decide what HTML it trusts.
    /// </remarks>
    public string? Text { get; set; }

    /// <summary>
    /// Whether this note has been struck through — the soft delete, as the
    /// legacy <c>struckthrough</c> column.
    /// </summary>
    /// <remarks>
    /// Required, where the legacy column is a nullable <c>BOOL</c>: every row
    /// in the session-012 snapshot leaves it blank, which reads as not struck
    /// through, so this schema makes the two states the two values of a
    /// required bool rather than keeping a third value that means the same as
    /// false.
    /// </remarks>
    public bool Struckthrough { get; set; }

    /// <summary>
    /// Who wrote the note — the Notes tab's "Creator" — as the legacy
    /// <c>user_id</c>. A bare nullable int, as
    /// <see cref="PersonCondition.AgencyId"/> is: there is no <c>user</c>
    /// table in this schema to point at.
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>When the note was written, as the legacy <c>created_at</c>.</summary>
    /// <remarks>
    /// A <see cref="DateTimeOffset"/> and not a <see cref="DateOnly"/> for the
    /// reason <see cref="PersonCondition.CreatedAt"/> is one: this is a moment
    /// rather than a day — the snapshot's values carry a time of day, down to
    /// the millisecond — and it carries its offset, so a note written in Utah
    /// says which hour it was written in rather than leaving a reader to
    /// assume the server's zone.
    ///
    /// Required, and set where the note is recorded. It is not an audit
    /// column: nothing maintains it afterwards, and the legacy table has no
    /// <c>updated_at</c> to pair with it because a note is never edited.
    /// </remarks>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// The exposure this note is about, where it is about one, as the legacy
    /// <c>exposure_id</c> — the legacy lets a note hang off an exposure as
    /// well as off a report. A bare nullable int and not a navigation.
    /// </summary>
    public int? ExposureId { get; set; }

    /// <summary>
    /// The case morbidity report this note is against, as the legacy
    /// <c>person_condition_id</c>; null where none is set. A bare nullable int
    /// and not a navigation, as <see cref="Exposure.PersonConditionId"/> is,
    /// set from the report in the route the note is recorded through.
    /// </summary>
    public int? PersonConditionId { get; set; }

    /// <summary>
    /// The facility this note is about, where it is about one, as the legacy
    /// <c>facility_id</c>. A bare nullable int: there is no <c>facility</c>
    /// table in this schema to point at.
    /// </summary>
    public int? FacilityId { get; set; }

    /// <summary>
    /// Which kind of note this is — Clinical or Administrative — as the legacy
    /// <c>note_type</c> column holds it, in the vocabulary the functional
    /// mapping states.
    /// </summary>
    /// <remarks>
    /// A plain code string, as the Clinical and Laboratory slices hold their
    /// coded fields, with no reference table behind it: the two values are a
    /// fixed vocabulary rather than an installation-configurable list, and
    /// what an offered value may be is the API tier's to say.
    /// </remarks>
    public string? NoteType { get; set; }
}
