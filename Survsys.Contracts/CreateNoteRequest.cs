namespace Survsys.Contracts;

/// <summary>
/// A note to record against a case morbidity report: what
/// <c>POST /api/person-conditions/{id}/notes</c> takes.
/// </summary>
/// <remarks>
/// The writable columns of the legacy <c>note</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, less the identity the server assigns, as the Notes
/// tab's Notes section records one —
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps the section.
///
/// <strong>The author is not here either.</strong> A note is recorded under the
/// signed-in user who wrote it, as an attachment is filed under its uploader:
/// a caller that could name the author could name somebody else.
///
/// <strong>The report and the moment are not here.</strong> A note is recorded
/// against the report in the route, so the server sets
/// <c>person_condition_id</c> from it rather than taking it on the wire, as
/// the exposure slice sets an exposure's report; <c>created_at</c> is the
/// server's clock, because a caller that could name the moment could name one
/// the note was not written at. <c>struckthrough</c> is not here either: a
/// note is recorded as standing, and striking it through is
/// <see cref="UpdateNoteStrikethroughRequest"/>'s.
///
/// <see cref="Text"/> is plain text. The legacy editor is a rich text editor
/// whose HTML the legacy column holds; this release stores what a text area
/// gave it, which round-trips through the same column without this tier having
/// to decide what HTML it trusts.
/// </remarks>
/// <param name="Text">What the note says. A note with nothing in it is refused.</param>
/// <param name="NoteType">
/// Which kind of note this is — the legacy <c>clinical</c> or
/// <c>administrative</c> code. The API tier validates it against the two,
/// refusing anything else.
/// </param>
/// <param name="ExposureId">
/// The exposure this note is about, where it is about one, as the legacy
/// <c>exposure_id</c>; null for a note about the report itself.
/// </param>
/// <param name="Private">
/// Whether only its author may see the note.
/// </param>
public record CreateNoteRequest(
    string? Text = null,
    string? NoteType = null,
    int? ExposureId = null,
    bool Private = false);
