namespace Survsys.Contracts;

/// <summary>
/// A note, as the API tier returns it: what
/// <c>GET /api/person-conditions/{id}/notes</c> returns one row of, and what
/// <c>POST /api/person-conditions/{id}/notes</c> and
/// <c>PUT /api/notes/{id}/strikethrough</c> answer with.
/// </summary>
/// <remarks>
/// The legacy <c>note</c> table's columns, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, as the Notes tab's note list shows them — creator,
/// note type, created date, the text, and the strikethrough checkbox —
/// mapped in
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>.
///
/// A struck-through note is returned like any other, with
/// <see cref="Struckthrough"/> true: strikethrough is a soft delete, so a
/// struck note stays on the list and the screen shows it struck. The report is
/// here as well as in the route, so the one shape serves the list and the two
/// single-note answers.
/// </remarks>
/// <param name="Id">The note's identifier, as the strikethrough route names it.</param>
/// <param name="PersonConditionId">The case morbidity report this note is against, or null where none is set.</param>
/// <param name="ExposureId">The exposure this note is about, or null for a note about the report itself.</param>
/// <param name="FacilityId">The facility this note is about, or null where none is recorded.</param>
/// <param name="UserId">Who wrote the note — the list's "Creator" — or null where nobody is named.</param>
/// <param name="NoteType">Which kind of note this is: the legacy <c>clinical</c> or <c>administrative</c> code.</param>
/// <param name="Text">What the note says, as plain text.</param>
/// <param name="Struckthrough">Whether the note has been struck through, which is this release's soft delete.</param>
/// <param name="CreatedAt">When the note was written, with the offset it was written at.</param>
public record NoteResponse(
    int Id,
    int? PersonConditionId,
    int? ExposureId,
    int? FacilityId,
    int? UserId,
    string? NoteType,
    string? Text,
    bool Struckthrough,
    DateTimeOffset CreatedAt);
