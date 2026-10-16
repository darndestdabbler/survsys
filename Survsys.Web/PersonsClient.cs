using System.Globalization;
using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// What a user typed into a person search form.
/// </summary>
/// <remarks>
/// The same six criteria the API tier takes, declared here because this tier
/// references <c>Survsys.Contracts</c> alone: the API tier's own criteria type
/// is bound from a query string and belongs to that tier, and a screen may not
/// reach past the wire shapes for anything. This is what a screen binds its
/// boxes to, and <see cref="PersonsClient.Page"/> turns it into the query
/// string.
///
/// Every criterion is optional, and one nobody typed is left out of the
/// request rather than sent empty — which matters for the date of birth, since
/// the API tier reads that one as a date and <c>?birthDate=</c> is not one.
/// </remarks>
/// <param name="LastName">A prefix of the person's last name.</param>
/// <param name="FirstName">A prefix of the person's first name.</param>
/// <param name="MiddleName">A prefix of the person's middle name.</param>
/// <param name="BirthDate">The person's date of birth, exactly.</param>
/// <param name="Phone">Any part of one of the person's telephone numbers.</param>
/// <param name="BirthGender">The legacy code for the sex recorded at birth.</param>
public sealed record PersonSearchCriteria(
    string? LastName = null,
    string? FirstName = null,
    string? MiddleName = null,
    DateOnly? BirthDate = null,
    string? Phone = null,
    string? BirthGender = null)
{
    /// <summary>Whether nothing has been typed into the form at all.</summary>
    /// <remarks>
    /// A screen reads this to decide whether a search is worth making, and to
    /// tell "no criteria" apart from "no matches" when it has nothing to show.
    /// </remarks>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(LastName)
        && string.IsNullOrWhiteSpace(FirstName)
        && string.IsNullOrWhiteSpace(MiddleName)
        && BirthDate is null
        && string.IsNullOrWhiteSpace(Phone)
        && string.IsNullOrWhiteSpace(BirthGender);
}

/// <summary>
/// The application tier's reader and writer of <c>/api/persons</c>: the people
/// matching a search, and a person put on file.
/// </summary>
/// <remarks>
/// It is the only thing in this tier that speaks HTTP about people, and it
/// swallows no failure silently: the two <see cref="ApiAsk"/>s below each say
/// which of the two things went wrong, because a screen that could not search
/// and a screen that could not create a person are two different sentences for
/// a user to read.
/// </remarks>
public sealed class PersonsClient(HttpClient http)
{
    /// <summary>How a failure to search starts.</summary>
    public const string SearchFailurePrefix = "The people could not be loaded";

    /// <summary>How a failure to read one person starts.</summary>
    public const string FindFailurePrefix = "The person could not be loaded";

    /// <summary>How a failure to create a person starts.</summary>
    public const string CreateFailurePrefix = "The person could not be created";

    private static readonly ApiAsk Searching = new(SearchFailurePrefix, "a page of people");

    private static readonly ApiAsk Finding = new(FindFailurePrefix, "the person's record");

    private static readonly ApiAsk Creating = new(CreateFailurePrefix, "the person created");

    /// <summary>
    /// One page of the people matching the criteria, or why there is none.
    /// </summary>
    public Task<Asked<PagedResult<PersonResponse>>> Page(
        PersonSearchCriteria criteria,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        Searching.Get<PagedResult<PersonResponse>>(http, Url(criteria, page, pageSize), cancellationToken);

    /// <summary>
    /// One person's whole demographic record, or why there is none — an
    /// identifier nobody has among the reasons, which the API tier answers
    /// with a 404.
    /// </summary>
    /// <remarks>
    /// What the creation flow's second screen needs: it is about one person,
    /// and it says who at the top so that a user can see who they are
    /// reporting on. It reads the detail rather than the list shape because
    /// the detail is what one person's own endpoint returns, and a screen does
    /// not search for a person it already has the identifier of.
    /// </remarks>
    public Task<Asked<PersonDetailResponse>> Find(
        int id,
        CancellationToken cancellationToken = default) =>
        Finding.Get<PersonDetailResponse>(http, $"api/persons/{id}", cancellationToken);

    /// <summary>
    /// Puts a person on file and answers with them, or with why none was
    /// created.
    /// </summary>
    public Task<Asked<PersonResponse>> Create(
        CreatePersonRequest person,
        CancellationToken cancellationToken = default) =>
        Creating.Post<CreatePersonRequest, PersonResponse>(
            http, "api/persons", person, cancellationToken);

    /// <summary>
    /// The URL a search asks for: the page, and every criterion that was
    /// typed.
    /// </summary>
    /// <remarks>
    /// A criterion nobody typed is left out altogether rather than sent as an
    /// empty value, which is what the API tier's contract asks for and what
    /// keeps an untouched date box from being read as a date that will not
    /// parse.
    ///
    /// Every value is escaped. A surname with an ampersand in it is a surname
    /// and not a second parameter, and one with a plus in it is a plus rather
    /// than a space.
    ///
    /// The date is written as <c>yyyy-MM-dd</c> under the invariant culture,
    /// because that is what the other tier parses it with — a date formatted
    /// in the browser's culture would arrive as a date in another month.
    ///
    /// Public because the unit suite proves it — the escaping and the absent
    /// criteria are the part of this client most worth proving, and proving
    /// them through a stubbed transport would be proving them through
    /// something else. Nothing outside this class calls it.
    /// </remarks>
    public static string Url(PersonSearchCriteria criteria, int page, int pageSize)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var asked = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}",
        };

        Typed("lastName", criteria.LastName);
        Typed("firstName", criteria.FirstName);
        Typed("middleName", criteria.MiddleName);
        Typed("phone", criteria.Phone);
        Typed("birthGender", criteria.BirthGender);

        if (criteria.BirthDate is { } birthDate)
        {
            asked.Add($"birthDate={birthDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        }

        return $"api/persons?{string.Join('&', asked)}";

        void Typed(string criterion, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                asked.Add($"{criterion}={Uri.EscapeDataString(value.Trim())}");
            }
        }
    }
}
