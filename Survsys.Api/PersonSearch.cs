namespace Survsys.Api;

/// <summary>
/// What a caller is looking a person up by: the criteria the legacy person
/// search form carries, bound from the query string of
/// <c>GET /api/persons</c>.
/// </summary>
/// <remarks>
/// The six fields are the legacy form's own, as
/// <c>../nedss/docs/functional/epitrax-core-workflows.md</c> lists them for
/// both the People screen and step one of CMR creation: the three name parts,
/// the date of birth, a telephone number and the sex recorded at birth. The
/// legacy form has a seventh — a free-text "Person name (Last, First,
/// Middle)" box beside the three name fields — and it is deliberately not
/// here: one way to search is one rule to keep.
///
/// A <c>Survsys.Api</c> type and not a <c>Survsys.Contracts</c> one, which is
/// the opposite of where a request body would live, and for a reason: this is
/// bound from a query string rather than carried in a body, so it is part of
/// how this tier reads a URL and not part of the wire shape a client
/// serialises. <c>Survsys.Web</c> builds the query string from what a user
/// typed and never constructs one of these.
///
/// What the criteria mean, and where each is proved. Absent criteria filter
/// nothing, so no criteria at all is the unfiltered page this endpoint
/// returned before search existed. The three name parts match by prefix —
/// typing <c>Rod</c> finds Rodriguez — because that is what a surname box is
/// for, and because the index on <c>persons(last_name, first_name)</c> can be
/// sought into for a prefix and not for a substring. The date of birth and the
/// birth gender match exactly. The telephone matches on digits alone, so that
/// a number typed <c>(801) 555-1234</c> finds one stored as area code
/// <c>801</c> and number <c>5551234</c>, and matches as a substring so that a
/// number typed with or without its area code finds the person either way.
///
/// A blank criterion is a criterion nobody gave, which is what
/// <see cref="Trimmed"/> is for — with one asymmetry worth knowing about. The
/// date of birth is the only typed criterion here, so it is bound and parsed
/// by the framework before any of this runs: <c>?birthDate=</c> is a value
/// that is not a date and is answered with a 400, where <c>?lastName=</c> is a
/// surname nobody typed. A caller leaves a criterion it has nothing for out of
/// the query string, which is what <c>Survsys.Web</c>'s client does with all
/// six.
///
/// There is no scoring and no fuzzy matching: the legacy
/// <c>person_score()</c> function, the <c>person_search</c> view and the
/// trigram indexes behind them are duplicate detection, which is a later
/// session's, and the Score column the legacy results table shows comes from
/// them. What this says is whether a person matches, not how well.
/// </remarks>
/// <param name="LastName">A prefix of the person's last name.</param>
/// <param name="FirstName">A prefix of the person's first name.</param>
/// <param name="MiddleName">A prefix of the person's middle name.</param>
/// <param name="BirthDate">The person's date of birth, exactly.</param>
/// <param name="Phone">
/// Any part of one of the person's telephone numbers, in whatever punctuation
/// it was typed with.
/// </param>
/// <param name="BirthGender">The legacy code for the sex recorded at birth.</param>
public sealed record PersonSearch(
    string? LastName = null,
    string? FirstName = null,
    string? MiddleName = null,
    DateOnly? BirthDate = null,
    string? Phone = null,
    string? BirthGender = null)
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
        Trimmed() is { LastName: null, FirstName: null, MiddleName: null, BirthDate: null,
            Phone: null, BirthGender: null };

    /// <summary>
    /// These criteria with every blank one turned into an absent one and the
    /// rest trimmed of the spaces a form submits around them.
    /// </summary>
    /// <remarks>
    /// The query asks for this once and then reads the criteria plainly:
    /// <c>is { Length: &gt; 0 }</c> means something was typed, which it does
    /// not mean of a raw bound value, because <c>?lastName=</c> and
    /// <c>?lastName=%20</c> both arrive as a string that is there and says
    /// nothing. A record in, a record out: nothing is mutated, and the bound
    /// values stay exactly as the caller sent them.
    /// </remarks>
    public PersonSearch Trimmed() => new(
        Said(LastName),
        Said(FirstName),
        Said(MiddleName),
        BirthDate,
        Said(Phone),
        Said(BirthGender));

    /// <summary>
    /// The digits of the telephone criterion, or null where it holds none.
    /// </summary>
    /// <remarks>
    /// Null and not an empty string where the criterion is all punctuation:
    /// an empty substring matches every telephone there is, so <c>?phone=--</c>
    /// would otherwise narrow a search to people who have a telephone at all,
    /// which is not what anybody typing it asked for.
    /// </remarks>
    public string? PhoneDigits => Digits(Phone);

    /// <summary>
    /// The digits of a telephone number as typed, with everything else
    /// dropped, or null where there are none.
    /// </summary>
    /// <remarks>
    /// A number is the digits in it: <c>(801) 555-1234</c>,
    /// <c>801-555-1234</c> and <c>801 555 1234</c> are one number typed three
    /// ways, and the legacy schema stores the area code and the rest of the
    /// number as digits in two columns. Dropping everything else is what lets
    /// the three find the one.
    ///
    /// Only the ASCII digits count. A digit of another script is not one this
    /// rule keeps, because the stored numbers are ASCII and a match against
    /// anything else could only fail — quietly, if it were kept and compared.
    /// </remarks>
    public static string? Digits(string? typed)
    {
        if (string.IsNullOrEmpty(typed))
        {
            return null;
        }

        var digits = new char[typed.Length];
        var kept = 0;

        foreach (var character in typed)
        {
            if (character is >= '0' and <= '9')
            {
                digits[kept++] = character;
            }
        }

        return kept == 0 ? null : new string(digits, 0, kept);
    }

    /// <summary>
    /// A criterion trimmed, or null where nothing was said in it.
    /// </summary>
    private static string? Said(string? criterion) =>
        string.IsNullOrWhiteSpace(criterion) ? null : criterion.Trim();
}
