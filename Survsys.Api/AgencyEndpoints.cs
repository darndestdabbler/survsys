using Microsoft.AspNetCore.Http.HttpResults;
using Survsys.Contracts;

namespace Survsys.Api;

/// <summary>
/// <c>GET /api/agencies</c>: the public health agencies a case morbidity
/// report can be assigned to, which is what step two of the creation flow's
/// Investigating Agency dropdown is filled from.
/// </summary>
/// <remarks>
/// <strong>This list is a stand-in, and it says so here rather than leaving a
/// reader to find out.</strong> The legacy <c>agency</c> table exists in
/// EpiTrax and does not exist here yet: agencies, their jurisdictions and the
/// zip-code-to-agency mapping are a later session's, the one that owns the
/// reference-data administration screens. What stands in until then is the
/// list of agency <em>names</em> the legacy creation screen offered, as
/// <c>../nedss/docs/functional/epitrax-cmr-workflow.md</c> records them from
/// the screen itself.
///
/// Two consequences, both deliberate. The identifiers here are assigned by
/// this list and are <em>not</em> the legacy environment's: no snapshot under
/// <c>../nedss/docs/db-snapshots/</c> dumps the <c>agency</c> table, so the
/// legacy ids are not known — the one id those documents do record is
/// <c>agency_id</c> 24 for Bear River in one environment and 11 in another,
/// which is the clearest evidence available that an agency id is a fact about
/// an installation rather than about an agency. And the session that writes
/// the real table owns the identifiers from then on: a
/// <c>person_conditions.agency_id</c> written against this list is a reference
/// into it, and that session is the one that has to carry the rows across.
///
/// This is the one endpoint in this tier that reaches no database. There is no
/// <c>AddAgencies</c> to go with it for the same reason: there is no query to
/// register, so the list is a static field and the handler returns it.
///
/// The document's own heading says seventeen and then names sixteen. Sixteen
/// are served — the ones it names — because the seventeenth is most likely the
/// dropdown's "Please select" row, and an agency invented to make a count
/// match would be a fiction in a reference list.
/// </remarks>
public static class AgencyEndpoints
{
    /// <summary>
    /// Every agency a report can be assigned to, in name order — the order a
    /// dropdown shows them in, fixed here so that it is the same order on
    /// every request and in every tier.
    /// </summary>
    /// <remarks>
    /// Public because the unit suite proves it and because
    /// <see cref="PersonConditionEndpoints"/> asks <see cref="Find"/> whether
    /// an agency a caller named is one of these.
    /// </remarks>
    public static readonly IReadOnlyList<AgencyResponse> All =
    [
        new(1, "Bear River"),
        new(2, "Central Utah"),
        new(3, "Davis County"),
        new(4, "Out of State"),
        new(5, "Salt Lake County"),
        new(6, "San Juan County"),
        new(7, "Southeastern Utah"),
        new(8, "Southwest Utah"),
        new(9, "Summit County"),
        new(10, "Tooele County"),
        new(11, "TriCounty"),
        new(12, "Utah County"),
        new(13, "Utah Navajo"),
        new(14, "Utah State"),
        new(15, "Wasatch County"),
        new(16, "Weber-Morgan"),
    ];

    /// <summary>
    /// The agency with that identifier, or null where no agency has it.
    /// </summary>
    /// <remarks>
    /// Null is the answer and not an exception, as
    /// <c>PersonQueries.Find</c>'s is: an identifier nobody has is a question
    /// this tier can answer, and the caller decides what it means. The
    /// creation endpoint's answer is a 400, because an agency nobody has is a
    /// bad request body rather than a missing resource.
    /// </remarks>
    public static AgencyResponse? Find(int id) =>
        All.SingleOrDefault(agency => agency.Id == id);

    public static IEndpointRouteBuilder MapAgencies(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/agencies", GetAgencies)
            .WithName("GetAgencies")
            .WithSummary("The public health agencies a case morbidity report can be "
                + "assigned to, ordered by name.");

        return routes;
    }

    /// <remarks>
    /// It takes no page and no page size: sixteen rows are not a page, and a
    /// dropdown that had to ask for a second one would be a dropdown nobody
    /// could use. It still answers in <see cref="PagedResult{T}"/>, because
    /// every collection this tier puts on the wire comes back in that one
    /// shape and a client that had to read two shapes would be reading this
    /// tier's exceptions rather than its contract. The one page it returns
    /// holds everything, so the page size and the total are both the length of
    /// the list.
    /// </remarks>
    private static Ok<PagedResult<AgencyResponse>> GetAgencies() =>
        TypedResults.Ok(new PagedResult<AgencyResponse>(All, Paging.DefaultPage, All.Count, All.Count));
}
