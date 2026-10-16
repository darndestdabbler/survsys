namespace Survsys.Data;

/// <summary>
/// A file attached to a case morbidity report: what it is called, what it is,
/// how big it is, what it was filed under and when it arrived — everything
/// about an attachment except the bytes.
/// </summary>
/// <remarks>
/// The columns are EpiTrax's own <c>attachment</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, down to their snake_case names — so a later
/// migration of the legacy data reads straight across rather than through a
/// column-name translation nobody kept. The screen is the Notes tab's first
/// section, as
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps it: an attachment panel with a category, a file picker and a list that
/// says "No attachments found" until something is attached.
///
/// <strong>The metadata and the bytes are two tables, and that is the point of
/// this one.</strong> <see cref="AttachmentFileId"/> points at the
/// <see cref="AttachmentFile"/> row that holds the file itself, as the legacy
/// pair does, so listing a report's attachments reads this table and never the
/// megabytes beside it: a list of twenty files costs a few hundred bytes of
/// metadata rather than everything anybody ever uploaded.
///
/// Both ids are required: an attachment is filed against a report and
/// describes a stored file, and neither is optional for a row that means
/// anything. So are <see cref="Filename"/>, <see cref="ContentType"/> and
/// <see cref="Category"/>, where the legacy columns are nullable — an
/// attachment nobody can name, open or find under a category is not one this
/// tier stores.
///
/// <strong>What is left out.</strong> The legacy <c>height</c> and
/// <c>width</c> are written by a server that decodes the uploaded image, and
/// <c>attachment_file.data_text</c> by one that extracts its text; this tier
/// decodes nothing and extracts nothing, so three columns nothing would ever
/// write are not here. <c>outbreak_id</c>, <c>outbreak_update_id</c> and
/// <c>master_id</c> are not here either: there is no outbreak table and no
/// master record in this schema to point at, which is why
/// <see cref="Exposure"/> has no <c>outbreak_id</c> of its own, so an
/// attachment hangs off a report and nothing else.
///
/// There is no behaviour here and no navigation out: the bytes are read by
/// composing a query over <see cref="AttachmentFile"/> rather than by walking
/// a navigation the entities do not have.
/// </remarks>
public class Attachment
{
    public int Id { get; set; }

    /// <summary>
    /// What the file is called, as the legacy <c>filename</c> — the name the
    /// list shows and the name a download is offered under.
    /// </summary>
    public string? Filename { get; set; }

    /// <summary>
    /// What the file is, as the caller declared it and as the legacy
    /// <c>content_type</c> holds it — the media type a download answers under.
    /// </summary>
    /// <remarks>
    /// The declared type, stored as given. Nothing here reads the bytes to
    /// check that they are what the type says they are: which types this tier
    /// stores at all is the API tier's to say, and it says it over this value.
    /// </remarks>
    public string? ContentType { get; set; }

    /// <summary>
    /// How many bytes were stored, as the legacy <c>size</c>.
    /// </summary>
    /// <remarks>
    /// A <see cref="long"/> where the legacy column is an <c>INT</c>. The
    /// limit this tier accepts is nowhere near an int's bound, so nothing
    /// turns on the width today; a byte count is simply not a number to store
    /// narrowly, and widening it later would be a migration over a table of
    /// files.
    /// </remarks>
    public long Size { get; set; }

    /// <summary>
    /// What the attachment was filed under — the Attachments panel's Category
    /// — as the legacy <c>category</c> column holds it.
    /// </summary>
    /// <remarks>
    /// A plain code string, as the Clinical and Laboratory slices hold their
    /// coded fields, with no reference table behind it: the categories are the
    /// fixed list the legacy upload dialog offers rather than an
    /// installation-configurable one, and what an offered value may be is the
    /// API tier's to say.
    /// </remarks>
    public string? Category { get; set; }

    /// <summary>When the file was attached, as the legacy <c>created_at</c>.</summary>
    /// <remarks>
    /// A <see cref="DateTimeOffset"/> and not a <see cref="DateOnly"/> for the
    /// reason <see cref="Note.CreatedAt"/> is one: this is a moment rather
    /// than a day, and it carries its offset, so a file attached in Utah says
    /// which hour it arrived in rather than leaving a reader to assume the
    /// server's zone.
    ///
    /// Required, and set where the attachment is stored. It is not an audit
    /// column: nothing maintains it afterwards, and an attachment is never
    /// changed once it is stored.
    /// </remarks>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// The case morbidity report this file is attached to, as the legacy
    /// <c>person_condition_id</c>. Required, and set from the report in the
    /// route the file is uploaded through.
    /// </summary>
    public int PersonConditionId { get; set; }

    /// <summary>
    /// The <see cref="AttachmentFile"/> row holding the bytes, as the legacy
    /// <c>attachment_file_id</c>. Required: there is no attachment without a
    /// file.
    /// </summary>
    public int AttachmentFileId { get; set; }
}
