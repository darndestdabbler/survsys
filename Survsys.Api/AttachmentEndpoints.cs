using System.Collections.Frozen;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>POST /api/person-conditions/{id}/attachments</c>: attaches one file to a
/// report. <c>GET /api/person-conditions/{id}/attachments</c>: that report's
/// attachments, newest first and paged, metadata only. And
/// <c>GET /api/attachments/{id}/content</c>: the stored bytes of one, under
/// the content type and filename they were stored with.
/// </summary>
/// <remarks>
/// Modelled on <see cref="NoteEndpoints"/>, slice for slice. The
/// entity-to-wire mapping lives here, as the other slices' does and for the
/// same reason: <c>Survsys.Data</c> may not reference <c>Survsys.Contracts</c>.
/// The handlers are handed <see cref="AttachmentQueries"/> and not the
/// <c>DbContext</c>, because the architecture test lets no
/// <c>Survsys.Data</c> type into an endpoint signature at all.
///
/// An attachment is scoped to the report in the route: the server sets
/// <c>person_condition_id</c> from it, the filename, content type and size
/// from the uploaded file, and <c>created_at</c> from the clock, taking none
/// of them on the wire, as the note slice sets a note's report and moment.
///
/// <strong>The list never reads the bytes.</strong> They live in
/// <c>attachment_files</c>, which only the download selects from, so listing a
/// report's attachments costs their names and sizes rather than everything
/// anybody ever uploaded.
///
/// <strong>What this slice refuses, it refuses before the database.</strong>
/// <see cref="WhyNotAnAttachment"/> is the whole of the size limit and the
/// content-type check, and it is pure, so the unit suite proves it. The route
/// also carries a request body size limit of its own, because the limit this
/// tier states has to be the limit a caller meets: without it the server's own
/// default would cut a large upload off first, and the caller would read a
/// bare refusal instead of the sentence naming the field.
///
/// There is no PUT and no DELETE. An attachment is stored once, and what the
/// session plan's section asks for is upload, listing and download.
/// </remarks>
public static class AttachmentEndpoints
{
    /// <summary>
    /// The category a general piece of correspondence is filed under.
    /// </summary>
    /// <remarks>
    /// The vocabulary is the five values the legacy upload dialog offers, as
    /// <c>legacy/nedss/src/main/webapp/views/person_condition/pc_attachment_upload.xhtml</c>
    /// under <c>../nedss</c> declares them, down to the codes it submits.
    /// Consts here and not a reference table, for the reason
    /// <see cref="NoteEndpoints.ClinicalNoteType"/> is a const: a category is
    /// a fixed vocabulary that only this slice reads, where a case status is
    /// read by everything that touches a report.
    /// </remarks>
    public const string CorrespondenceCategory = "correspondence";

    /// <summary>The category laboratory results are filed under.</summary>
    public const string LabCategory = "lab";

    /// <summary>The category a letter is filed under.</summary>
    public const string LetterCategory = "letter";

    /// <summary>The category an x-ray is filed under.</summary>
    public const string XRayCategory = "x-ray";

    /// <summary>The category anything else is filed under.</summary>
    public const string OtherCategory = "other";

    /// <summary>
    /// The largest file this tier stores: 30 MB, which is the limit the legacy
    /// upload dialog states and enforces.
    /// </summary>
    /// <remarks>
    /// The same number the legacy's own <c>sizeLimit</c> is, to the byte, so a
    /// file that could be attached to a legacy report can be attached to one
    /// here.
    /// </remarks>
    public const long MaxBytes = 31_457_280;

    /// <summary>
    /// What the multipart envelope around a file is allowed to add: the
    /// boundaries, the headers and the category field beside it.
    /// </summary>
    /// <remarks>
    /// The request body limit is this plus <see cref="MaxBytes"/>, so a file of
    /// exactly the stated limit arrives whole and is accepted, and one over it
    /// is refused by <see cref="WhyNotAnAttachment"/> in words rather than by
    /// the server in silence. A kilobyte is far more than the envelope needs
    /// and far less than another file.
    /// </remarks>
    public const long MultipartEnvelopeBytes = 1024;

    /// <summary>
    /// The content types this tier stores, matched without regard to case.
    /// </summary>
    /// <remarks>
    /// Short, and each entry is here because one of the five categories is
    /// about it: PDF, plain text and CSV for correspondence and laboratory
    /// results, Word — the old binary format and the XML one — for letters,
    /// and PNG, JPEG, GIF and TIFF for x-rays and the scans that stand in for
    /// them. An allowlist and not a blocklist: a type nobody has a reason to
    /// attach is refused, which is the opposite of listing the types somebody
    /// might attack with.
    ///
    /// The check is over the type the caller declares and nothing more. No
    /// byte here is examined, so a file whose contents disagree with its
    /// declared type is stored as declared and handed back as declared.
    /// </remarks>
    public static readonly FrozenSet<string> StoredContentTypes = new[]
    {
        "application/pdf",
        "text/plain",
        "text/csv",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "image/png",
        "image/jpeg",
        "image/gif",
        "image/tiff",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>The categories this tier files an attachment under.</summary>
    /// <remarks>
    /// The five codes, as a set, so <see cref="WhyNotAnAttachment"/> asks once
    /// and the sentence it gives can list them.
    /// </remarks>
    public static readonly FrozenSet<string> Categories = new[]
    {
        CorrespondenceCategory,
        LabCategory,
        LetterCategory,
        XRayCategory,
        OtherCategory,
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The projection from a stored attachment to its wire shape.</summary>
    /// <remarks>
    /// An <see cref="Expression"/>, as the other slices' projections are: EF
    /// Core translates it into the <c>SELECT</c> of the list, and a unit test
    /// compiles and invokes it to prove what it carries. It names no column of
    /// <c>attachment_files</c>, which is what keeps the list off the bytes.
    /// One shape serves the list and the upload. Public because the unit suite
    /// proves it.
    /// </remarks>
    public static readonly Expression<Func<Attachment, AttachmentResponse>> AttachmentToResponse =
        attachment => new AttachmentResponse(
            attachment.Id,
            attachment.PersonConditionId,
            attachment.Filename!,
            attachment.ContentType!,
            attachment.Size,
            attachment.Category!,
            attachment.CreatedAt);

    /// <summary>
    /// <see cref="AttachmentToResponse"/> compiled, for the upload that
    /// answers with the attachment it just stored rather than reading it back
    /// through a query EF would translate.
    /// </summary>
    public static readonly Func<Attachment, AttachmentResponse> Respond =
        AttachmentToResponse.Compile();

    /// <summary>
    /// Why the file offered is not one this endpoint can store, as far as can
    /// be said without the database, or null when nothing is wrong with it.
    /// </summary>
    /// <remarks>
    /// The shape <see cref="NoteEndpoints.WhyNotANote"/> has: a pure function
    /// the unit suite proves, with the endpoint turning the sentence into a 400
    /// that names the field. Whether the report exists is not here, because
    /// that needs a query. What is here is the size limit and the content-type
    /// check the session plan's section asks for, and the category: a file
    /// with no name, an empty one, one over <see cref="MaxBytes"/>, one
    /// declaring a type this tier does not store, and one filed under a
    /// category that is not one of the five.
    ///
    /// The declared type is read up to its first <c>;</c>, so
    /// <c>text/plain; charset=utf-8</c> is the <c>text/plain</c> the set
    /// holds: a caller that names the media type correctly and says what
    /// encoding it is in has not named a different type.
    ///
    /// Public because the unit suite proves it; nothing outside this file
    /// calls it.
    /// </remarks>
    public static string? WhyNotAnAttachment(
        string? filename,
        string? contentType,
        long size,
        string? category)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return "filename is missing, and an attachment is a file somebody has to be able "
                + "to find again: send the file under the name it has.";
        }

        if (size <= 0)
        {
            return "the file is empty, and an empty file is nothing to attach: send a file "
                + "with something in it.";
        }

        if (size > MaxBytes)
        {
            return $"the file is {size} bytes, and the largest this tier stores is {MaxBytes}. "
                + "Attach a smaller file.";
        }

        if (!StoredContentTypes.Contains(MediaTypeOf(contentType)))
        {
            return $"contentType '{contentType}' is not a kind of file this tier stores. Give "
                + $"one of {string.Join(", ", StoredContentTypes)}.";
        }

        if (!Categories.Contains(category ?? string.Empty))
        {
            return $"category '{category}' is not one an attachment is filed under. Give "
                + $"{string.Join(", ", Categories)}.";
        }

        return null;
    }

    /// <summary>
    /// The media type a declared content type names, without the parameters
    /// after it.
    /// </summary>
    /// <remarks>
    /// Private because <see cref="WhyNotAnAttachment"/> is the question
    /// callers ask. A null or absent type reads as the empty string, which is
    /// in no allowlist.
    /// </remarks>
    private static string MediaTypeOf(string? contentType) =>
        contentType is null ? string.Empty : contentType.Split(';')[0].Trim();

    /// <summary>
    /// Registers what the attachment endpoints need of the container. Kept
    /// here with the endpoints so the slice's wiring is in one place.
    /// </summary>
    public static IServiceCollection AddAttachments(this IServiceCollection services) =>
        services.AddScoped<AttachmentQueries>();

    public static IEndpointRouteBuilder MapAttachments(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/person-conditions/{id:int}/attachments", UploadAttachment)
            .WithName("UploadAttachment")
            .WithSummary("Attaches one file to a case morbidity report.")

            // This tier is an API that reads no cookie and is no browser
            // form's target, so there is no session for a forged post to ride
            // on and no token for the caller to carry.
            .DisableAntiforgery()

            // The limit this tier states is the limit a caller meets: without
            // this the server's own default body size would refuse a large
            // upload before the handler ran, and the caller would read a bare
            // refusal rather than the sentence naming the field.
            .WithMetadata(new RequestSizeLimitAttribute(MaxBytes + MultipartEnvelopeBytes));

        routes.MapGet("/api/person-conditions/{id:int}/attachments", GetAttachments)
            .WithName("GetAttachments")
            .WithSummary("A case morbidity report's attachments, newest first and paged.");

        routes.MapGet("/api/attachments/{id:int}/content", GetAttachmentContent)
            .WithName("GetAttachmentContent")
            .WithSummary("The stored bytes of one attachment.");

        return routes;
    }

    private static async Task<Results<Created<AttachmentResponse>, ProblemHttpResult>>
        UploadAttachment(
            AttachmentQueries attachments,
            int id,
            IFormFile file,
            [FromForm] string? category,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (WhyNotAnAttachment(file.FileName, file.ContentType, file.Length, category) is { } why)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The file offered is not one this endpoint can store.");
        }

        if (!await attachments.HasReport(id, cancellationToken))
        {
            return NoSuchReport(id);
        }

        var stored = await attachments.Store(id, file, category!, cancellationToken);

        return TypedResults.Created($"/api/attachments/{stored.Id}", stored);
    }

    private static async Task<Results<Ok<PagedResult<AttachmentResponse>>, ProblemHttpResult>>
        GetAttachments(
            AttachmentQueries attachments,
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

        if (!await attachments.HasReport(id, cancellationToken))
        {
            return NoSuchReport(id);
        }

        return TypedResults.Ok(await attachments.Page(id, page, pageSize, cancellationToken));
    }

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>>
        GetAttachmentContent(
            AttachmentQueries attachments,
            int id,
            CancellationToken cancellationToken = default)
    {
        if (await attachments.Content(id, cancellationToken) is not { } content)
        {
            return TypedResults.Problem(
                detail: $"No attachment has id {id}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such attachment.");
        }

        return TypedResults.File(
            content.Bytes,
            content.ContentType,
            fileDownloadName: content.Filename);
    }

    private static ProblemHttpResult NoSuchReport(int id) =>
        TypedResults.Problem(
            detail: $"No case morbidity report has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such case morbidity report.");
}

/// <summary>
/// One stored file on its way out: its bytes and the two things a download
/// needs to hand them over under.
/// </summary>
/// <remarks>
/// A type of this tier's and not a <c>Survsys.Data</c> entity, because the
/// download reads one row of each table and has no use for either entity
/// whole. Internal, in this file, as <see cref="AttachmentQueries"/> is.
/// </remarks>
/// <param name="Bytes">The file as it was stored.</param>
/// <param name="ContentType">What it was declared to be when it was stored.</param>
/// <param name="Filename">The name it is offered under.</param>
internal sealed record StoredFile(byte[] Bytes, string ContentType, string Filename);

/// <summary>
/// The attachment queries and the storing, and the only thing in this tier
/// that holds the <c>DbContext</c> for them.
/// </summary>
/// <remarks>
/// Internal, and in this file rather than its own, for the reason
/// <c>NoteQueries</c> is: it exists only to serve
/// <see cref="AttachmentEndpoints"/>, and it is the named step between an
/// endpoint signature and <c>Survsys.Data</c> that the architecture test
/// requires.
/// </remarks>
internal sealed class AttachmentQueries(SurvsysDbContext database)
{
    /// <summary>
    /// Whether a report has that id — false is the answer the endpoint turns
    /// into a 404, because the report is the resource the route names.
    /// </summary>
    public Task<bool> HasReport(int reportId, CancellationToken cancellationToken) =>
        database.PersonConditions.AnyAsync(report => report.Id == reportId, cancellationToken);

    /// <summary>
    /// Stores the file and the row that describes it, and answers with the
    /// attachment as it was stored.
    /// </summary>
    /// <remarks>
    /// Two tables and one transaction: the file row is written first so the
    /// metadata can point at it, and either both land or neither does. Bytes
    /// with nothing describing them would be invisible, and metadata naming a
    /// file that is not there would be worse — the list would offer a download
    /// that cannot be served.
    ///
    /// The report is the route's, the filename, content type and size are the
    /// uploaded file's own, and the moment is the clock's, as the note slice
    /// reads a note's scope off the report rather than off the wire. The
    /// length is read from the file rather than from the bytes copied, because
    /// it is the length the endpoint already held to the limit.
    /// </remarks>
    public async Task<AttachmentResponse> Store(
        int reportId,
        IFormFile file,
        string category,
        CancellationToken cancellationToken)
    {
        var bytes = await Bytes(file, cancellationToken);

        await using var transaction = await database.Database.BeginTransactionAsync(
            cancellationToken);

        var stored = new AttachmentFile { Data = bytes };

        database.AttachmentFiles.Add(stored);
        await database.SaveChangesAsync(cancellationToken);

        var attachment = new Attachment
        {
            PersonConditionId = reportId,
            AttachmentFileId = stored.Id,
            Filename = file.FileName,
            ContentType = file.ContentType,
            Size = file.Length,
            Category = category,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        database.Attachments.Add(attachment);
        await database.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return AttachmentEndpoints.Respond(attachment);
    }

    /// <summary>
    /// One page of a report's attachments, newest first, or the page as it is
    /// where there are fewer attachments than that.
    /// </summary>
    /// <remarks>
    /// Newest first, as the Attachments panel reads, and then by id descending
    /// — a total order, so an attachment cannot appear on two pages or on
    /// neither when two arrived in the same instant. Metadata only: the
    /// projection names no column of <c>attachment_files</c>, so this query
    /// never pulls a file out of the database.
    /// </remarks>
    public async Task<PagedResult<AttachmentResponse>> Page(
        int reportId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var attachments = database.Attachments
            .Where(attachment => attachment.PersonConditionId == reportId);

        var total = await attachments.CountAsync(cancellationToken);

        var rows = await attachments
            .OrderByDescending(attachment => attachment.CreatedAt)
            .ThenByDescending(attachment => attachment.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(AttachmentEndpoints.AttachmentToResponse)
            .ToListAsync(cancellationToken);

        return new PagedResult<AttachmentResponse>(rows, page, pageSize, total);
    }

    /// <summary>
    /// The stored bytes of one attachment with what a download hands them over
    /// under, or null where no attachment has that id.
    /// </summary>
    /// <remarks>
    /// One query over the pair, joined on the id the metadata holds: the bytes
    /// are selected here and nowhere else in this slice.
    /// </remarks>
    public async Task<StoredFile?> Content(int attachmentId, CancellationToken cancellationToken) =>
        await (from attachment in database.Attachments
               join file in database.AttachmentFiles
                   on attachment.AttachmentFileId equals file.Id
               where attachment.Id == attachmentId
               select new StoredFile(file.Data!, attachment.ContentType!, attachment.Filename!))
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// The uploaded file, read into memory.
    /// </summary>
    /// <remarks>
    /// Into memory and not streamed into the database, because the column is a
    /// <c>varbinary(max)</c> an EF property is set from whole, and because the
    /// endpoint has already held the length to <see cref="AttachmentEndpoints.MaxBytes"/>
    /// — so what is read here is bounded before a byte of it is. The buffer is
    /// sized from that length, so a file arrives in one allocation rather than
    /// in a dozen doublings.
    /// </remarks>
    private static async Task<byte[]> Bytes(IFormFile file, CancellationToken cancellationToken)
    {
        await using var uploaded = file.OpenReadStream();
        using var buffer = new MemoryStream(capacity: (int)file.Length);

        await uploaded.CopyToAsync(buffer, cancellationToken);

        return buffer.ToArray();
    }
}
