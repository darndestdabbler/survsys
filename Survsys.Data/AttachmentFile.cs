namespace Survsys.Data;

/// <summary>
/// The bytes of an attached file, and nothing else.
/// </summary>
/// <remarks>
/// The columns are EpiTrax's own <c>attachment_file</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, down to their snake_case names.
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// records what the pair is for: <c>attachment</c> carries what a list shows,
/// and this table "stores the actual file content".
///
/// <strong>A row of its own, so that reading the list is not reading the
/// files.</strong> <see cref="Attachment"/> points here by
/// <see cref="Attachment.AttachmentFileId"/>, and that split is kept from the
/// legacy because it is what lets a report's attachment list be one cheap
/// query: nothing selects this table except the download of one file.
///
/// The legacy <c>data_text</c> is not here. It holds the text a server
/// extracted from the file; this tier extracts nothing, so a column nothing
/// would ever write is left out rather than carried.
///
/// There is no behaviour here and no navigation out.
/// </remarks>
public class AttachmentFile
{
    public int Id { get; set; }

    /// <summary>
    /// The file as it was uploaded, byte for byte, as the legacy <c>data</c>
    /// column holds it.
    /// </summary>
    /// <remarks>
    /// Required: a file row with no bytes is not a file. The bytes live in the
    /// database as the legacy's do — there is no disk path and no blob store
    /// here, and so nothing a deployment has to provision beside the database
    /// it already has.
    /// </remarks>
    public byte[]? Data { get; set; }
}
