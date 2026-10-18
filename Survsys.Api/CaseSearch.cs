namespace Survsys.Api;

/// <summary>
/// What a caller is looking a case morbidity report up by: the criteria the
/// Events screen carries, bound from the query string of
/// <c>GET /api/cases</c>.
/// </summary>
/// <remarks>
/// The sibling of <see cref="PersonSearch"/>, and a <c>Survsys.Api</c> type
/// for the same reason: it is bound from a query string rather than carried in
/// a body, so it is part of how this tier reads a URL and not part of the wire
/// shape a client serialises. <c>Survsys.Web</c> builds the query string from
/// what a user typed and never constructs one of these.
///
/// The criteria are the ones the legacy Events list filters by that this
/// schema can answer, as <c>../nedss/docs/functional/epitrax-core-workflows.md</c>
/// lists them: the record number, the patient's last and first names, the
/// disease, the investigating agency, the workflow status, the case status
/// and the event date range. Absent criteria filter nothing, so no criteria at
/// all is every case on file, newest first. The record number and the two name
/// parts match by prefix, exactly as the people search does: typing
/// <c>2026</c> finds every report of the year and typing <c>Rod</c> finds
/// Rodriguez, and the comparison is one an index can be sought into. The
/// condition, the agency, the person, the workflow state and the case status
/// match exactly, because each is a key or a code a list offered rather than
/// something typed.
///
/// The date range is over the event onset date and inclusive at both ends.
/// The legacy Events list's one date filter is "Event date after/before", and
/// the event date its results table shows is the onset; the date a report was
/// first reported to public health and the date it was created are facts about
/// the paperwork, where the onset is a fact about the case, which is what an
/// investigator narrowing a year to a month is asking about. Either end may be
/// given alone: a <c>from</c> with no <c>to</c> is every case on or after it.
///
/// <see cref="PersonId"/> is not a box on the Events screen. It is here so
/// that one endpoint lists a person's cases on the person's own screen, which
/// is the same question — the cases matching a criterion — asked with a
/// different one, and a second endpoint for it would be a second query to keep
/// in step with this one.
///
/// A blank criterion is a criterion nobody gave, which is what
/// <see cref="Trimmed"/> is for, and the typed criteria are bound and parsed by
/// the framework before any of this runs: <c>?conditionId=</c> or
/// <c>?onsetFrom=</c> with a value that is not an integer or a date is
/// answered with a 400, where <c>?lastName=</c> is a surname nobody typed. A
/// caller leaves a criterion it has nothing for out of the query string,
/// which is what <c>Survsys.Web</c>'s client does with all ten.
/// </remarks>
/// <param name="RecordNumber">A prefix of the report's record number.</param>
/// <param name="LastName">A prefix of the patient's last name.</param>
/// <param name="FirstName">A prefix of the patient's first name.</param>
/// <param name="ConditionId">The condition the report is on, exactly.</param>
/// <param name="AgencyId">The agency investigating the report, exactly.</param>
/// <param name="PersonId">The patient the report is about, exactly.</param>
/// <param name="WorkflowState">The legacy workflow code the report is in, exactly.</param>
/// <param name="CaseStatus">The legacy case status code the report holds, exactly.</param>
/// <param name="OnsetFrom">The earliest event onset date to include, inclusive.</param>
/// <param name="OnsetTo">The latest event onset date to include, inclusive.</param>
public sealed record CaseSearch(
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
    /// <summary>
    /// Whether no criterion was given at all, in which case the page is
    /// unfiltered.
    /// </summary>
    /// <remarks>
    /// Read off the trimmed criteria, so a form that submitted its empty
    /// boxes — which is what a browser does with a text input nobody typed in
    /// — is empty here as well. Nothing in this tier depends on
    /// <see cref="IsEmpty"/> to decide whether to filter; the query applies
    /// each criterion that is there and leaves out each that is not, and this
    /// is for saying what happened rather than for deciding it.
    /// </remarks>
    public bool IsEmpty =>
        Trimmed() is
        {
            RecordNumber: null, LastName: null, FirstName: null,
            ConditionId: null, AgencyId: null, PersonId: null,
            WorkflowState: null, CaseStatus: null,
            OnsetFrom: null, OnsetTo: null,
        };

    /// <summary>
    /// These criteria with every blank one turned into an absent one and the
    /// rest trimmed of the spaces a form submits around them.
    /// </summary>
    /// <remarks>
    /// The query asks for this once and then reads the criteria plainly:
    /// <c>is { Length: &gt; 0 }</c> means something was typed, which it does
    /// not mean of a raw bound value, because <c>?lastName=</c> and
    /// <c>?lastName=%20</c> both arrive as a string that is there and says
    /// nothing. The typed criteria pass through as they are: a bound integer
    /// or date has nothing around it to trim. A record in, a record out:
    /// nothing is mutated, and the bound values stay exactly as the caller
    /// sent them.
    /// </remarks>
    public CaseSearch Trimmed() => new(
        Said(RecordNumber),
        Said(LastName),
        Said(FirstName),
        ConditionId,
        AgencyId,
        PersonId,
        Said(WorkflowState),
        Said(CaseStatus),
        OnsetFrom,
        OnsetTo);

    /// <summary>
    /// A criterion trimmed, or null where nothing was said in it.
    /// </summary>
    private static string? Said(string? criterion) =>
        string.IsNullOrWhiteSpace(criterion) ? null : criterion.Trim();
}
