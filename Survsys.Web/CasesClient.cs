using System.Globalization;
using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// What a user typed into the case search form.
/// </summary>
/// <remarks>
/// The same ten criteria the API tier takes, declared here because this tier
/// references <c>Survsys.Contracts</c> alone: the API tier's own criteria type
/// is bound from a query string and belongs to that tier, and a screen may not
/// reach past the wire shapes for anything. This is what the Events screen
/// binds its boxes to, what the person screen fills a <see cref="PersonId"/>
/// into, and what <see cref="CasesClient.Page"/> turns into the query string.
///
/// Every criterion is optional, and one nobody typed is left out of the
/// request rather than sent empty — which matters for the ids and the dates,
/// since the API tier reads those as integers and dates and
/// <c>?conditionId=</c> is not one.
/// </remarks>
/// <param name="RecordNumber">A prefix of the report's record number.</param>
/// <param name="LastName">A prefix of the patient's last name.</param>
/// <param name="FirstName">A prefix of the patient's first name.</param>
/// <param name="ConditionId">The condition the report is on.</param>
/// <param name="AgencyId">The agency investigating the report.</param>
/// <param name="PersonId">The patient the report is about.</param>
/// <param name="WorkflowState">The legacy workflow code the report is in.</param>
/// <param name="CaseStatus">The legacy case status code the report holds.</param>
/// <param name="OnsetFrom">The earliest event onset date to include, inclusive.</param>
/// <param name="OnsetTo">The latest event onset date to include, inclusive.</param>
public sealed record CaseSearchCriteria(
    string? RecordNumber = null,
    string? LastName = null,
    string? FirstName = null,
    int? ConditionId = null,
    int? AgencyId = null,
    int? PersonId = null,
    string? WorkflowState = null,
    string? CaseStatus = null,
    DateOnly? OnsetFrom = null,
    DateOnly? OnsetTo = null)
{
    /// <summary>Whether nothing has been typed into the form at all.</summary>
    /// <remarks>
    /// A screen reads this to tell "no criteria" apart from "no matches" when
    /// it has nothing to show; the empty search is a search the API tier
    /// answers, with every case on file.
    /// </remarks>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(RecordNumber)
        && string.IsNullOrWhiteSpace(LastName)
        && string.IsNullOrWhiteSpace(FirstName)
        && ConditionId is null
        && AgencyId is null
        && PersonId is null
        && string.IsNullOrWhiteSpace(WorkflowState)
        && string.IsNullOrWhiteSpace(CaseStatus)
        && OnsetFrom is null
        && OnsetTo is null;
}

/// <summary>
/// The application tier's reader of <c>/api/cases</c>: the case morbidity
/// reports matching a search, one page at a time.
/// </summary>
/// <remarks>
/// The sibling of <see cref="PersonsClient"/>, and the only thing in this tier
/// that speaks HTTP about the case search. It reads and never writes: opening
/// a case, moving it and classifying it stay with
/// <see cref="PersonConditionsClient"/>, and this answers the one question of
/// which cases match.
/// </remarks>
public sealed class CasesClient(HttpClient http)
{
    /// <summary>How a failure to search starts.</summary>
    public const string SearchFailurePrefix = "The cases could not be loaded";

    private static readonly ApiAsk Searching = new(SearchFailurePrefix, "a page of cases");

    /// <summary>
    /// One page of the cases matching the criteria, newest record number
    /// first, or why there is none.
    /// </summary>
    public Task<Asked<PagedResult<CaseSearchItemResponse>>> Page(
        CaseSearchCriteria criteria,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        Searching.Get<PagedResult<CaseSearchItemResponse>>(http, Url(criteria, page, pageSize), cancellationToken);

    /// <summary>
    /// The URL a search asks for: the page, and every criterion that was
    /// typed.
    /// </summary>
    /// <remarks>
    /// A criterion nobody typed is left out altogether rather than sent as an
    /// empty value, which is what the API tier's contract asks for and what
    /// keeps an untouched id or date box from being read as a value that will
    /// not parse.
    ///
    /// Every text value is escaped. A surname with an ampersand in it is a
    /// surname and not a second parameter, and one with a plus in it is a plus
    /// rather than a space. The ids and the dates are written under the
    /// invariant culture — the dates as <c>yyyy-MM-dd</c> — because that is
    /// what the other tier parses them with; a date formatted in the browser's
    /// culture would arrive as a date in another month.
    ///
    /// Public because the unit suite proves it — the escaping and the absent
    /// criteria are the part of this client most worth proving, and proving
    /// them through a stubbed transport would be proving them through
    /// something else. Nothing outside this class calls it.
    /// </remarks>
    public static string Url(CaseSearchCriteria criteria, int page, int pageSize)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var asked = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}",
        };

        Typed("recordNumber", criteria.RecordNumber);
        Typed("lastName", criteria.LastName);
        Typed("firstName", criteria.FirstName);
        Chosen("conditionId", criteria.ConditionId);
        Chosen("agencyId", criteria.AgencyId);
        Chosen("personId", criteria.PersonId);
        Typed("workflowState", criteria.WorkflowState);
        Typed("caseStatus", criteria.CaseStatus);
        Dated("onsetFrom", criteria.OnsetFrom);
        Dated("onsetTo", criteria.OnsetTo);

        return $"api/cases?{string.Join('&', asked)}";

        void Typed(string criterion, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                asked.Add($"{criterion}={Uri.EscapeDataString(value.Trim())}");
            }
        }

        void Chosen(string criterion, int? value)
        {
            if (value is { } chosen)
            {
                asked.Add($"{criterion}={chosen.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        void Dated(string criterion, DateOnly? value)
        {
            if (value is { } date)
            {
                asked.Add($"{criterion}={date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
            }
        }
    }
}
