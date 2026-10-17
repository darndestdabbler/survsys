namespace Survsys.Contracts;

/// <summary>
/// One attached file, as the API tier describes it: what
/// <c>GET /api/person-conditions/{id}/attachments</c> returns one row of, and
/// what <c>POST /api/person-conditions/{id}/attachments</c> answers with.
/// </summary>
/// <remarks>
/// The legacy <c>attachment</c> table's columns, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, as the Notes tab's Attachments section shows them —
/// a file's name, what it is, how big it is, what it was filed under and when
/// it arrived — mapped in
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>.
///
/// <strong>This is the metadata of a stored attachment and never its
/// bytes.</strong> The bytes are what
/// <c>GET /api/attachments/{id}/content</c> answers with, under the
/// <see cref="ContentType"/> and <see cref="Filename"/> recorded here, so a
/// list of a report's attachments costs the names and sizes and not the files
/// themselves — which is why the API tier keeps them in a table of their own.
///
/// The upload and the list answer with this one shape, as the note slice's one
/// shape serves its list and its two writes: a caller reads the attachment it
/// has just stored in the same words the list will give it.
///
/// There is no <c>CreateAttachmentRequest</c> beside this. An upload is a
/// multipart body — the file and its category are form fields, which is what a
/// browser sends and what the endpoint binds — so there is no JSON record for
/// a caller to fill in.
/// </remarks>
/// <param name="Id">The attachment's identifier, as the download route names it.</param>
/// <param name="PersonConditionId">The case morbidity report this file is attached to.</param>
/// <param name="Filename">What the file is called, and the name a download is offered under.</param>
/// <param name="ContentType">What the file is, as it was declared when it was uploaded.</param>
/// <param name="Size">How many bytes were stored.</param>
/// <param name="Category">What the file was filed under: one of the categories the API tier accepts.</param>
/// <param name="CreatedAt">When the file was attached, with the offset it was attached at.</param>
public record AttachmentResponse(
    int Id,
    int PersonConditionId,
    string Filename,
    string ContentType,
    long Size,
    string Category,
    DateTimeOffset CreatedAt);
