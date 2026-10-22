using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;
using Survsys.Messaging;

namespace Survsys.Api;

/// <summary>
/// <c>GET /api/ecr-documents</c>: the eCR queue, the eICRs and RRs taken in,
/// newest first, paged and filterable by feed. <c>GET /api/ecr-documents/{id}</c>:
/// one document with its pair, the eICR rendered through the HL7 CDA
/// stylesheet beside the RR's conditions and both documents' validation
/// findings.
/// </summary>
/// <remarks>
/// Read-only: the intake service fills the queue, and what a reviewer does
/// with a document is a later session's. Both are reads under <c>/api</c>, so
/// <see cref="PrivilegeRules"/> asks the case-viewing privilege of them, as it
/// does of the staged-message queue. As in <see cref="MessageQueueEndpoints"/>,
/// the handlers are handed <see cref="EcrQueries"/> and not the
/// <c>DbContext</c>, which the architecture test keeps out of an endpoint
/// signature.
/// </remarks>
public static class EcrEndpoints
{
    /// <summary>The projection from a stored document to its queue-summary wire shape.</summary>
    /// <remarks>
    /// An <see cref="Expression"/>, so EF Core translates the finding counts
    /// into the <c>SELECT</c>. Public because the unit suite proves it.
    /// </remarks>
    public static readonly Expression<Func<EcrDocument, EcrDocumentResponse>> DocumentToResponse =
        document => new EcrDocumentResponse(
            document.Id,
            document.Kind,
            document.Feed,
            document.DocumentId,
            document.VersionNumber,
            document.Source,
            document.ReceivedAt,
            document.Findings.Count(finding =>
                finding.Severity == CdaValidator.Fatal || finding.Severity == CdaValidator.Error),
            document.Findings.Count(finding => finding.Severity == CdaValidator.Warning));

    /// <summary>
    /// The conditions of one parsed RR as the document screen shows them.
    /// Public because the unit suite proves it.
    /// </summary>
    public static IReadOnlyList<EcrConditionResponse> Conditions(CdaDocument rr)
    {
        ArgumentNullException.ThrowIfNull(rr);

        return rr.Conditions
            .Select(condition => new EcrConditionResponse(
                rr.DocumentId,
                condition.Condition?.Code,
                condition.Condition?.DisplayName,
                condition.Determination?.Code,
                condition.Determination?.DisplayName,
                condition.Location?.Code,
                condition.Location?.DisplayName,
                condition.ResponsibleAgencyId,
                condition.ResponsibleAgencyName,
                condition.ReportingTimeframe))
            .ToList();
    }

    /// <summary>Registers what the eCR endpoints need of the container.</summary>
    public static IServiceCollection AddEcr(this IServiceCollection services) =>
        services.AddSingleton<CdaStylesheet>().AddScoped<EcrQueries>();

    public static IEndpointRouteBuilder MapEcr(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/ecr-documents", GetEcrDocuments)
            .WithName("GetEcrDocuments")
            .WithSummary("The eCR queue: eICRs and RRs taken in, newest first, paged and filterable by feed.");

        routes.MapGet("/api/ecr-documents/{id:int}", GetEcrDocument)
            .WithName("GetEcrDocument")
            .WithSummary("One eCR document: the eICR rendered, its RRs' conditions and the validation findings.");

        return routes;
    }

    private static async Task<Results<Ok<PagedResult<EcrDocumentResponse>>, ProblemHttpResult>> GetEcrDocuments(
        EcrQueries documents,
        string? feed = null,
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

        return TypedResults.Ok(await documents.Page(feed, page, pageSize, cancellationToken));
    }

    private static async Task<Results<Ok<EcrDocumentDetailResponse>, ProblemHttpResult>> GetEcrDocument(
        EcrQueries documents,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (await documents.Detail(id, cancellationToken) is not { } detail)
        {
            return TypedResults.Problem(
                detail: $"No eCR document has id {id}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such eCR document.");
        }

        return TypedResults.Ok(detail);
    }
}

/// <summary>The eCR endpoints' reads of the stored documents.</summary>
internal sealed class EcrQueries(SurvsysDbContext database, CdaParser parser, CdaStylesheet stylesheet)
{
    /// <summary>
    /// One page of the queue, newest first, with the id as the tie-break so a
    /// document cannot appear on two pages, narrowed to one feed when a feed is
    /// given.
    /// </summary>
    public async Task<PagedResult<EcrDocumentResponse>> Page(
        string? feed,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var documents = database.EcrDocuments.AsQueryable();

        if (!string.IsNullOrWhiteSpace(feed))
        {
            var wanted = feed.Trim();
            documents = documents.Where(document => document.Feed == wanted);
        }

        var total = await documents.CountAsync(cancellationToken);

        var rows = await documents
            .OrderByDescending(document => document.ReceivedAt)
            .ThenByDescending(document => document.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(EcrEndpoints.DocumentToResponse)
            .ToListAsync(cancellationToken);

        return new PagedResult<EcrDocumentResponse>(rows, page, pageSize, total);
    }

    /// <summary>
    /// One document with its pair, or null where no document has that id: for
    /// an eICR, the RRs that respond to it; for an RR, the eICR it responds to.
    /// </summary>
    public async Task<EcrDocumentDetailResponse?> Detail(int id, CancellationToken cancellationToken)
    {
        var summary = await database.EcrDocuments
            .Where(document => document.Id == id)
            .Select(EcrEndpoints.DocumentToResponse)
            .SingleOrDefaultAsync(cancellationToken);

        if (summary is null)
        {
            return null;
        }

        var documents = database.EcrDocuments.AsNoTracking().Include(document => document.Findings);
        var document = await documents.SingleAsync(document => document.Id == id, cancellationToken);

        EcrDocument? eicr;
        List<EcrDocument> rrs;
        if (document.Kind == EcrDocument.RrKind)
        {
            eicr = await EcrIntakeService.EicrOf(documents, document).FirstOrDefaultAsync(cancellationToken);
            rrs = [document];
        }
        else
        {
            eicr = document;
            rrs = await EcrIntakeService.ResponsesTo(documents, document).ToListAsync(cancellationToken);
        }

        var pair = eicr is null ? rrs : rrs.Prepend(eicr);
        var findings = pair
            .SelectMany(each => each.Findings
                .OrderBy(finding => finding.Id)
                .Select(finding => new EcrValidationFindingResponse(
                    each.Kind, each.DocumentId, finding.Source, finding.Severity, finding.Message, finding.Location)))
            .ToList();

        return new EcrDocumentDetailResponse(
            summary,
            eicr is null ? null : stylesheet.Render(eicr.RawXml),
            rrs.SelectMany(rr => EcrEndpoints.Conditions(parser.Read(rr.RawXml))).ToList(),
            findings);
    }
}
