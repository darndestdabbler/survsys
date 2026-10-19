using System.Net.Http.Headers;
using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of a report's attachments:
/// <c>GET /api/person-conditions/{id}/attachments</c>, and the multipart
/// <c>POST</c> beside it that attaches one file.
/// </summary>
/// <remarks>
/// It is the only thing in this tier that speaks HTTP about attachments, and
/// the failure handling is <see cref="ApiAsk"/>'s, as every client's is.
///
/// Two asks and two prefixes, as <see cref="NotesClient"/> has three: a user
/// whose list of attachments could not be loaded and one whose file would not
/// go up are two different sentences to read. The upload answers with the
/// attachment as it was stored, so the panel puts that at the top of what it
/// holds rather than asking for the list again.
///
/// <strong>Nothing here downloads a file.</strong> The bytes are
/// <c>GET /api/attachments/{id}/content</c>'s, and this tier does not hand
/// them on: a screen that offered the download would have to either give the
/// browser the API tier's address, which the production split does not allow,
/// or stream the file through this tier, and neither is the upload and listing
/// this session's section asks for.
/// </remarks>
public sealed class AttachmentsClient(HttpClient http)
{
    /// <summary>How a failure to list a report's attachments starts.</summary>
    public const string ListFailurePrefix = "The attachments could not be loaded";

    /// <summary>How a failure to attach a file starts.</summary>
    public const string UploadFailurePrefix = "The file could not be attached";

    /// <summary>
    /// The page size <see cref="All"/> walks a report's attachments at: the
    /// largest the API tier allows, so a report's attachments arrive in as few
    /// asks as they can.
    /// </summary>
    public const int FullPage = 200;

    /// <summary>
    /// The largest file this tier will read out of the browser and send: the
    /// API tier's own stated limit, 30 MB.
    /// </summary>
    /// <remarks>
    /// Restated here because this tier cannot reach the one that states it —
    /// <c>Survsys.Web</c> references <c>Survsys.Contracts</c> alone — and
    /// because reading a chosen file requires a ceiling to read up to, which
    /// has to be a number held on this side.
    ///
    /// It is not a second authority. The API tier refuses a file over its own
    /// limit whatever this says; this is the point at which a user hears about
    /// it on the screen in front of them rather than after sending thirty
    /// megabytes to be told no.
    /// </remarks>
    public const long MaxBytes = 31_457_280;

    /// <summary>
    /// The form field the upload endpoint binds the file from. The endpoint's
    /// parameter is called <c>file</c>, and a part under any other name would
    /// not be found.
    /// </summary>
    private const string FilePart = "file";

    /// <summary>The form field the upload endpoint binds the category from.</summary>
    private const string CategoryPart = "category";

    private static readonly ApiAsk Listing = new(ListFailurePrefix, "a page of attachments");

    private static readonly ApiAsk Uploading =
        new(UploadFailurePrefix, "the attachment as it was stored");

    /// <summary>
    /// One page of a report's attachments, newest first, or why there is none.
    /// </summary>
    public Task<Asked<PagedResult<AttachmentResponse>>> Page(
        int reportId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        PageOf($"api/person-conditions/{reportId}", page, pageSize, cancellationToken);

    /// <summary>
    /// Every attachment of a report, newest first, read page by page until the
    /// total the API states has arrived, or why there is none.
    /// </summary>
    /// <remarks>
    /// A report's attachments are listed whole on the Notes tab, so the panel
    /// asks for all of them rather than for a page, exactly as
    /// <see cref="NotesClient.All"/> does: this walks <see cref="Page"/> at the
    /// largest page size the API allows and stops when as many have arrived as
    /// the API said there were, or when a page comes back empty. A page that
    /// fails is the failure of the whole, in the list's own words, because a
    /// list with a hole in it is not the list.
    /// </remarks>
    public Task<Asked<IReadOnlyList<AttachmentResponse>>> All(
        int reportId,
        CancellationToken cancellationToken = default) =>
        AllOf($"api/person-conditions/{reportId}", cancellationToken);

    /// <summary>
    /// Every attachment of an outbreak, newest first, read as <see cref="All"/>
    /// reads a report's, or why there is none.
    /// </summary>
    public Task<Asked<IReadOnlyList<AttachmentResponse>>> AllForOutbreak(
        int outbreakId,
        CancellationToken cancellationToken = default) =>
        AllOf($"api/outbreaks/{outbreakId}", cancellationToken);

    private Task<Asked<PagedResult<AttachmentResponse>>> PageOf(
        string owner,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        Listing.Get<PagedResult<AttachmentResponse>>(
            http,
            $"{owner}/attachments?page={page}&pageSize={pageSize}",
            cancellationToken);

    private async Task<Asked<IReadOnlyList<AttachmentResponse>>> AllOf(
        string owner,
        CancellationToken cancellationToken)
    {
        var all = new List<AttachmentResponse>();

        for (var page = 1; ; page++)
        {
            var asked = await PageOf(owner, page, FullPage, cancellationToken);

            if (asked.Value is not { } listed)
            {
                return Asked<IReadOnlyList<AttachmentResponse>>.Failed(
                    asked.Failure ?? $"{ListFailurePrefix}: page {page} could not be read.");
            }

            all.AddRange(listed.Items);

            if (listed.Items.Count == 0 || all.Count >= listed.TotalCount)
            {
                return Asked<IReadOnlyList<AttachmentResponse>>.Of(all);
            }
        }
    }

    /// <summary>
    /// Attaches one file to a report and answers with the attachment as it was
    /// stored, or with why nothing was stored.
    /// </summary>
    /// <remarks>
    /// The multipart body the endpoint binds: the file under
    /// <see cref="FilePart"/>, carrying the name and the declared type the
    /// browser gave, and the category beside it under
    /// <see cref="CategoryPart"/>. The stream is read as it is sent and is the
    /// caller's to dispose, because the caller is the one that opened it.
    ///
    /// A declared type this tier cannot even parse as a media type is left off
    /// the part rather than guessed at, which the API tier then refuses by the
    /// same rule it refuses any type it does not store.
    /// </remarks>
    public Task<Asked<AttachmentResponse>> Attach(
        int reportId,
        string filename,
        string? contentType,
        string? category,
        Stream content,
        CancellationToken cancellationToken = default) =>
        AttachTo($"api/person-conditions/{reportId}", filename, contentType, category, content, cancellationToken);

    /// <summary>
    /// Attaches one file to an outbreak, as <see cref="Attach"/> attaches one
    /// to a report, and answers with the attachment as it was stored, or with
    /// why nothing was stored.
    /// </summary>
    public Task<Asked<AttachmentResponse>> AttachToOutbreak(
        int outbreakId,
        string filename,
        string? contentType,
        string? category,
        Stream content,
        CancellationToken cancellationToken = default) =>
        AttachTo($"api/outbreaks/{outbreakId}", filename, contentType, category, content, cancellationToken);

    private async Task<Asked<AttachmentResponse>> AttachTo(
        string owner,
        string filename,
        string? contentType,
        string? category,
        Stream content,
        CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        using var file = new StreamContent(content);

        if (MediaTypeHeaderValue.TryParse(contentType, out var declared))
        {
            file.Headers.ContentType = declared;
        }

        form.Add(file, FilePart, filename);
        form.Add(new StringContent(category ?? string.Empty), CategoryPart);

        return await Uploading.PostContent<AttachmentResponse>(
            http,
            $"{owner}/attachments",
            form,
            cancellationToken);
    }
}
