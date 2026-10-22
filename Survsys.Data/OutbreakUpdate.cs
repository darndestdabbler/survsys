namespace Survsys.Data;

/// <summary>
/// One entry in an outbreak's activity log: a timeline update somebody added
/// to an outbreak, with who added it and when.
/// </summary>
/// <remarks>
/// EpiTrax's own <c>outbreak_update</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>: the outbreak, when it was added, who added it, the
/// text and a type. Written and never changed, as <see cref="AccessRecord"/>
/// is: the activity log is a record of what happened, so an entry is appended
/// and left.
/// </remarks>
public class OutbreakUpdate
{
    public int Id { get; set; }

    /// <summary>The outbreak the entry is on.</summary>
    public int OutbreakId { get; set; }

    /// <summary>When the entry was added, as the legacy <c>created_at</c>.</summary>
    /// <remarks>
    /// A <see cref="DateTimeOffset"/>, as <see cref="Note.CreatedAt"/> is:
    /// a moment that carries its offset, stamped where the entry is appended.
    /// </remarks>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Who added the entry, as the legacy <c>user_id</c>.</summary>
    public int UserId { get; set; }

    /// <summary>
    /// What the entry says, as the legacy <c>description</c> — free text, left
    /// unbounded as <see cref="Note.Text"/> is; null where none was given.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The kind of entry, as the legacy <c>type</c> int; 0 until moved. A
    /// plain coded int, as the legacy holds it, with no reference table behind
    /// it.
    /// </summary>
    public int Type { get; set; }
}
