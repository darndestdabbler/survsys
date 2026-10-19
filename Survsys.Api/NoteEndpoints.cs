using System.Linq.Expressions;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>POST /api/person-conditions/{id}/notes</c>: records a note against a
/// report. <c>GET /api/person-conditions/{id}/notes</c>: that report's notes,
/// newest first and paged. And <c>PUT /api/notes/{id}/strikethrough</c>: the
/// soft delete, which strikes a note through or restores it.
/// </summary>
/// <remarks>
/// Modelled on <see cref="ExposureEndpoints"/>, slice for slice. The
/// entity-to-wire mapping lives here, as the other slices' does and for the
/// same reason: <c>Survsys.Data</c> may not reference <c>Survsys.Contracts</c>.
/// The handlers are handed <see cref="NoteQueries"/> and not the
/// <c>DbContext</c>, because the architecture test lets no
/// <c>Survsys.Data</c> type into an endpoint signature at all.
///
/// A note is scoped to the report in the route: the server sets
/// <c>person_condition_id</c> to that report and <c>created_at</c> from the
/// clock, taking neither on the wire, as the exposure slice sets an exposure's
/// report.
///
/// <strong>There is no PUT over a note's text and no DELETE.</strong>
/// Strikethrough is the soft delete the Notes tab offers, and it is the one
/// mutation a note takes: a struck note is still returned by the list, with
/// <see cref="NoteResponse.Struckthrough"/> true, and the screen shows it
/// struck. Nothing here hides a struck note, because hiding it would make the
/// soft delete a hard one with extra steps.
///
/// A private note is the one thing the list does hide: it is recorded under
/// the signed-in user rather than the user on the wire, and listed only to
/// that user, the page's total counting only what the caller may see.
/// </remarks>
public static class NoteEndpoints
{
    /// <summary>
    /// The code a clinical note is stored under, as the legacy
    /// <c>note.note_type</c> holds it.
    /// </summary>
    /// <remarks>
    /// The vocabulary is the two values the Notes tab's type dropdown offers,
    /// as
    /// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
    /// records them, and the session-012 snapshot's notes are
    /// <c>administrative</c>. Two consts here and not a reference table or a
    /// rule class in <c>Survsys.Data</c>: a note type is a fixed vocabulary of
    /// two that only this slice reads, where a case status is read by
    /// everything that touches a report.
    /// </remarks>
    public const string ClinicalNoteType = "clinical";

    /// <summary>The code an administrative note is stored under.</summary>
    public const string AdministrativeNoteType = "administrative";

    /// <summary>The projection from a stored note to its wire shape.</summary>
    /// <remarks>
    /// An <see cref="Expression"/>, as the other slices' projections are: EF
    /// Core translates it into the <c>SELECT</c> of the list, and a unit test
    /// compiles and invokes it to prove what it carries. One shape serves the
    /// list, the recording and the strikethrough. Public because the unit
    /// suite proves it.
    /// </remarks>
    public static readonly Expression<Func<Note, NoteResponse>> NoteToResponse =
        note => new NoteResponse(
            note.Id,
            note.PersonConditionId,
            note.ExposureId,
            note.FacilityId,
            note.UserId,
            note.NoteType,
            note.Text,
            note.Struckthrough,
            note.CreatedAt,
            note.Private);

    /// <summary>
    /// <see cref="NoteToResponse"/> compiled, for the two writes that answer
    /// with the note they just saved rather than reading it back through a
    /// query EF would translate.
    /// </summary>
    public static readonly Func<Note, NoteResponse> Respond = NoteToResponse.Compile();

    /// <summary>
    /// Why the note asked to be recorded is not one this endpoint can record,
    /// as far as can be said without the database, or null when nothing is
    /// wrong with it.
    /// </summary>
    /// <remarks>
    /// The shape <see cref="ExposureEndpoints.WhyNotAnExposure"/> has: a pure
    /// function the unit suite proves, with the endpoint turning the sentence
    /// into a 400 that names the field. Whether the report exists is not here,
    /// because that needs a query. What is here is what is wrong with the note
    /// on its face: a text with nothing in it, because a note is what somebody
    /// wrote and an empty one records nothing; and a note type outside the two
    /// the vocabulary knows, the absent one included — a note that does not
    /// say which kind it is cannot be shown under either heading of the tab
    /// that lists it.
    ///
    /// Public because the unit suite proves it; nothing outside this file
    /// calls it.
    /// </remarks>
    public static string? WhyNotANote(CreateNoteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return "text is missing, and a note is what somebody wrote: give the text of the "
                + "note.";
        }

        if (!IsANoteType(request.NoteType))
        {
            return $"noteType '{request.NoteType}' is not a kind of note this tier records. "
                + $"Give {ClinicalNoteType} or {AdministrativeNoteType}.";
        }

        return null;
    }

    /// <summary>
    /// Whether an offered code is one of the two kinds of note. Private
    /// because <see cref="WhyNotANote"/> is the question callers ask.
    /// </summary>
    private static bool IsANoteType(string? offeredCode) =>
        offeredCode is ClinicalNoteType or AdministrativeNoteType;

    /// <summary>
    /// Registers what the note endpoints need of the container. Kept here with
    /// the endpoints so the slice's wiring is in one place.
    /// </summary>
    public static IServiceCollection AddNotes(this IServiceCollection services) =>
        services.AddScoped<NoteQueries>();

    public static IEndpointRouteBuilder MapNotes(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/person-conditions/{id:int}/notes", CreateNote)
            .WithName("CreateNote")
            .WithSummary("Records a note against a case morbidity report.");

        routes.MapGet("/api/person-conditions/{id:int}/notes", GetNotes)
            .WithName("GetNotes")
            .WithSummary("A case morbidity report's notes, newest first and paged.");

        routes.MapPut("/api/notes/{id:int}/strikethrough", SetStrikethrough)
            .WithName("SetNoteStrikethrough")
            .WithSummary("Strikes a note through, or restores one struck through.");

        return routes;
    }

    private static async Task<Results<Created<NoteResponse>, ProblemHttpResult>> CreateNote(
        NoteQueries notes,
        ClaimsPrincipal user,
        int id,
        CreateNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotANote(request) is { } why)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The note asked for is not one this endpoint can record.");
        }

        if (!await notes.HasReport(id, cancellationToken))
        {
            return NoSuchReport(id);
        }

        var recorded = await notes.Add(id, request, AccountEndpoints.UserIdOf(user), cancellationToken);

        return TypedResults.Created($"/api/notes/{recorded.Id}", recorded);
    }

    private static async Task<Results<Ok<PagedResult<NoteResponse>>, ProblemHttpResult>> GetNotes(
        NoteQueries notes,
        ClaimsPrincipal user,
        int id,
        int page = Paging.DefaultPage,
        int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (Paging.WhyNotAPage(page, pageSize) is { } why)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The page asked for is not one this endpoint can return.");
        }

        if (!await notes.HasReport(id, cancellationToken))
        {
            return NoSuchReport(id);
        }

        return TypedResults.Ok(await notes.Page(id, AccountEndpoints.UserIdOf(user), page, pageSize, cancellationToken));
    }

    private static async Task<Results<Ok<NoteResponse>, ProblemHttpResult>> SetStrikethrough(
        NoteQueries notes,
        int id,
        UpdateNoteStrikethroughRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await notes.Strike(id, request.Struckthrough, cancellationToken) is not { } struck)
        {
            return TypedResults.Problem(
                detail: $"No note has id {id}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such note.");
        }

        return TypedResults.Ok(struck);
    }

    private static ProblemHttpResult NoSuchReport(int id) =>
        TypedResults.Problem(
            detail: $"No case morbidity report has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such case morbidity report.");
}

/// <summary>
/// The note queries and the recording, and the only thing in this tier that
/// holds the <c>DbContext</c> for them.
/// </summary>
/// <remarks>
/// Internal, and in this file rather than its own, for the reason
/// <c>ExposureQueries</c> is: it exists only to serve
/// <see cref="NoteEndpoints"/>, and it is the named step between an endpoint
/// signature and <c>Survsys.Data</c> that the architecture test requires.
/// </remarks>
internal sealed class NoteQueries(SurvsysDbContext database)
{
    /// <summary>
    /// Whether a report has that id — false is the answer the endpoint turns
    /// into a 404, because the report is the resource the route names.
    /// </summary>
    public Task<bool> HasReport(int reportId, CancellationToken cancellationToken) =>
        database.PersonConditions.AnyAsync(report => report.Id == reportId, cancellationToken);

    /// <summary>
    /// Records the note against the report and answers with it as it was
    /// stored.
    /// </summary>
    /// <remarks>
    /// The report is the route's and the moment is the clock's, as the
    /// exposure slice reads an exposure's scope off the report rather than off
    /// the wire. A note is recorded standing: <c>struckthrough</c> is left
    /// false, and striking it through is <see cref="Strike"/>'s. One table and
    /// one save, so there is no transaction to open — the exposure slice opens
    /// one because it writes four tables.
    /// </remarks>
    public async Task<NoteResponse> Add(
        int reportId,
        CreateNoteRequest request,
        int? callerId,
        CancellationToken cancellationToken)
    {
        var note = new Note
        {
            PersonConditionId = reportId,
            ExposureId = request.ExposureId,
            UserId = request.Private ? callerId : request.UserId,
            Private = request.Private,
            NoteType = request.NoteType,
            Text = request.Text,
            Struckthrough = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        database.Notes.Add(note);
        await database.SaveChangesAsync(cancellationToken);

        return NoteEndpoints.Respond(note);
    }

    /// <summary>
    /// One page of a report's notes, newest first, or the page as it is where
    /// there are fewer notes than that.
    /// </summary>
    /// <remarks>
    /// Newest first, as the Notes tab's list reads, and then by id descending
    /// — a total order, so a note cannot appear on two pages or on neither
    /// when two were written in the same instant. Struck-through notes are
    /// among them: strikethrough is a soft delete, and the list shows a struck
    /// note struck.
    /// </remarks>
    public async Task<PagedResult<NoteResponse>> Page(
        int reportId,
        int? callerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var notes = database.Notes.Where(note => note.PersonConditionId == reportId
            && (!note.Private || note.UserId == callerId));

        var total = await notes.CountAsync(cancellationToken);

        var rows = await notes
            .OrderByDescending(note => note.CreatedAt)
            .ThenByDescending(note => note.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(NoteEndpoints.NoteToResponse)
            .ToListAsync(cancellationToken);

        return new PagedResult<NoteResponse>(rows, page, pageSize, total);
    }

    /// <summary>
    /// Strikes a note through or restores it, and answers with the note as it
    /// now stands, or null where no note has that id.
    /// </summary>
    public async Task<NoteResponse?> Strike(
        int noteId,
        bool struckthrough,
        CancellationToken cancellationToken)
    {
        var note = await database.Notes
            .SingleOrDefaultAsync(candidate => candidate.Id == noteId, cancellationToken);

        if (note is null)
        {
            return null;
        }

        note.Struckthrough = struckthrough;
        await database.SaveChangesAsync(cancellationToken);

        return NoteEndpoints.Respond(note);
    }
}
