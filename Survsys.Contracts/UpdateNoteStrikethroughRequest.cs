namespace Survsys.Contracts;

/// <summary>
/// Whether a note is struck through: what
/// <c>PUT /api/notes/{id}/strikethrough</c> takes.
/// </summary>
/// <remarks>
/// One writable field, where the note id is the route's, as
/// <see cref="UpdateCaseStatusRequest"/> takes one with the report id in the
/// route.
///
/// <strong>This is the soft delete, and it is the only mutation a note
/// takes.</strong> A note is never edited and never deleted: it is struck
/// through, stays on the list and is shown struck, which is what the Notes
/// tab's strikethrough checkbox does. The field is a bool and not a verb
/// because the checkbox clears as well as sets — false restores a note struck
/// through by mistake.
/// </remarks>
/// <param name="Struckthrough">
/// True to strike the note through, false to restore it.
/// </param>
public record UpdateNoteStrikethroughRequest(bool Struckthrough);
