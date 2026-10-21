using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of a report's notes:
/// <c>GET</c> and <c>POST /api/person-conditions/{id}/notes</c>, and
/// <c>PUT /api/notes/{id}/strikethrough</c> for the soft delete.
/// </summary>
/// <remarks>
/// It is the only thing in this tier that speaks HTTP about notes, and the
/// failure handling is <see cref="ApiAsk"/>'s, as every client's is.
///
/// Three asks and three prefixes, as <see cref="ExposuresClient"/> has,
/// because a user whose list of notes could not be loaded, one whose new note
/// was refused and one whose strikethrough did not take are three different
/// sentences to read. The record and the strikethrough both answer with the
/// note as it now stands, so the tab replaces what it holds rather than asking
/// for the list again.
/// </remarks>
public sealed class NotesClient(HttpClient http)
{
    /// <summary>How a failure to list a report's notes starts.</summary>
    public const string ListFailurePrefix = "The notes could not be loaded";

    /// <summary>How a failure to record a new note starts.</summary>
    public const string RecordFailurePrefix = "The note could not be recorded";

    /// <summary>How a failure to strike a note through or restore it starts.</summary>
    public const string StrikeFailurePrefix = "The note could not be changed";

    /// <summary>
    /// The page size <see cref="All"/> walks a report's notes at: the largest
    /// the API tier allows, so a report's notes arrive in as few asks as they
    /// can.
    /// </summary>
    public const int FullPage = 200;

    private static readonly ApiAsk Listing = new(ListFailurePrefix, "a page of notes");

    private static readonly ApiAsk Recording = new(RecordFailurePrefix, "the note recorded");

    private static readonly ApiAsk Striking = new(StrikeFailurePrefix, "the note as it now stands");

    /// <summary>One page of a report's notes, newest first, or why there is none.</summary>
    public Task<Asked<PagedResult<NoteResponse>>> Page(
        int reportId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        Listing.Get<PagedResult<NoteResponse>>(
            http,
            $"api/person-conditions/{reportId}/notes?page={page}&pageSize={pageSize}",
            cancellationToken);

    /// <summary>
    /// Every note of a report, newest first, read page by page until the total
    /// the API states has arrived, or why there is none.
    /// </summary>
    /// <remarks>
    /// A report's notes are listed whole on the Notes tab, so the tab asks for
    /// all of them rather than for a page, exactly as
    /// <see cref="ExposuresClient.All"/> does: this walks <see cref="Page"/>
    /// at the largest page size the API allows and stops when as many have
    /// arrived as the API said there were, or when a page comes back empty. A
    /// page that fails is the failure of the whole, in the list's own words,
    /// because a list with a hole in it is not the list. Struck-through notes
    /// are among them — strikethrough is a soft delete, and the tab shows a
    /// struck note struck. Here rather than in the tab so the stub transport
    /// can prove the second page is asked for.
    /// </remarks>
    public async Task<Asked<IReadOnlyList<NoteResponse>>> All(
        int reportId,
        CancellationToken cancellationToken = default)
    {
        var all = new List<NoteResponse>();

        for (var page = 1; ; page++)
        {
            var asked = await Page(reportId, page, FullPage, cancellationToken);

            if (asked.Value is not { } listed)
            {
                return Asked<IReadOnlyList<NoteResponse>>.Failed(
                    asked.Failure ?? $"{ListFailurePrefix}: page {page} could not be read.");
            }

            all.AddRange(listed.Items);

            if (listed.Items.Count == 0 || all.Count >= listed.TotalCount)
            {
                return Asked<IReadOnlyList<NoteResponse>>.Of(all);
            }
        }
    }

    /// <summary>
    /// Records a note against a report and answers with it as it was stored,
    /// or with why none was recorded.
    /// </summary>
    public Task<Asked<NoteResponse>> Record(
        int reportId,
        CreateNoteRequest note,
        CancellationToken cancellationToken = default) =>
        Recording.Post<CreateNoteRequest, NoteResponse>(
            http, $"api/person-conditions/{reportId}/notes", note, cancellationToken);

    /// <summary>
    /// Strikes a note through, or restores one struck through, and answers
    /// with the note as it now stands.
    /// </summary>
    /// <remarks>
    /// The soft delete, and the only change this tier makes to a stored note:
    /// there is no method here that edits a note's text, because there is no
    /// endpoint that does.
    /// </remarks>
    public Task<Asked<NoteResponse>> Strike(
        int noteId,
        bool struckthrough,
        CancellationToken cancellationToken = default) =>
        Striking.Put<UpdateNoteStrikethroughRequest, NoteResponse>(
            http,
            $"api/notes/{noteId}/strikethrough",
            new UpdateNoteStrikethroughRequest(struckthrough),
            cancellationToken);
}
