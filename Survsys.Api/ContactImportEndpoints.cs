using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>POST /api/person-conditions/{id}/contacts/import</c>: parses and
/// validates a batch CSV of contacts against a report and answers with the
/// rows it would accept and the rows it rejects.
/// </summary>
/// <remarks>
/// The legacy Contacts tab's "Import" button, as
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// lists it, writes the batch to the <c>contact_import_batch</c>,
/// <c>contact_import_row</c> and <c>contact_import_answer</c> tables. None of
/// those is modelled here and nothing is written: the parsing is
/// <see cref="ContactImportParser"/>'s, pure, and the one thing this endpoint
/// asks the database is whether the report in the route exists, because a
/// batch is imported against a report and a report that is not there is a
/// 404. A persisted import, and a contact that becomes a person, are later
/// work.
///
/// The handler is handed <see cref="ContactImportQueries"/> and not the
/// <c>DbContext</c>, because the architecture test lets no <c>Survsys.Data</c>
/// type into an endpoint signature at all. The answer is a 200 and not a 201,
/// because nothing was created.
/// </remarks>
public static class ContactImportEndpoints
{
    /// <summary>
    /// Registers what the contact import needs of the container. Kept here
    /// with the endpoint so the slice's wiring is in one place.
    /// </summary>
    public static IServiceCollection AddContactImport(this IServiceCollection services) =>
        services.AddScoped<ContactImportQueries>();

    public static IEndpointRouteBuilder MapContactImport(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/person-conditions/{id:int}/contacts/import", ImportContacts)
            .WithName("ImportContacts")
            .WithSummary("Parses a batch CSV of contacts against a case morbidity report, persisting nothing.");

        return routes;
    }

    private static async Task<Results<Ok<ContactImportResponse>, ProblemHttpResult>> ImportContacts(
        ContactImportQueries reports,
        int id,
        ImportContactsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await reports.HasReport(id, cancellationToken))
        {
            return TypedResults.Problem(
                detail: $"No case morbidity report has id {id}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such case morbidity report.");
        }

        return TypedResults.Ok(ContactImportParser.Parse(request.Csv));
    }
}

/// <summary>
/// The one question the contact import asks the database, and the only thing
/// in this tier that holds the <c>DbContext</c> for it.
/// </summary>
/// <remarks>
/// Internal, and in this file rather than its own, for the reason
/// <c>LabQueries</c> is: it exists only to serve
/// <see cref="ContactImportEndpoints"/>, and it is the named step between an
/// endpoint signature and <c>Survsys.Data</c> that the architecture test
/// requires. It reads and never writes.
/// </remarks>
internal sealed class ContactImportQueries(SurvsysDbContext database)
{
    /// <summary>
    /// Whether a report has that id — false is the answer the endpoint turns
    /// into a 404.
    /// </summary>
    public Task<bool> HasReport(int reportId, CancellationToken cancellationToken) =>
        database.PersonConditions.AnyAsync(report => report.Id == reportId, cancellationToken);
}
