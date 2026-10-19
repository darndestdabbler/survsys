namespace Survsys.Messaging;

/// <summary>
/// What <see cref="PersonMatchRules.Classify"/> makes of a candidate: whether
/// it is the same person for certain, a plausible match a person must confirm,
/// or not a match.
/// </summary>
public enum PersonMatchKind
{
    /// <summary>The same person: take the match without review.</summary>
    Exact,

    /// <summary>A plausible match a Data Entry reviewer must confirm.</summary>
    Fuzzy,

    /// <summary>Not a match.</summary>
    None,
}

/// <summary>
/// A person the search returned as a candidate for the incoming message, as
/// plain in-memory data: the person, the match score EpiTrax scored them, and
/// the fields the classification reads past the score. None of this is a
/// Survsys.Data entity; the caller projects whatever it holds into this record.
/// </summary>
/// <param name="PersonId">The candidate person.</param>
/// <param name="Score">The EpiTrax match score, from 0 to 100.</param>
/// <param name="DateOfBirth">The candidate's date of birth, where it has one.</param>
/// <param name="MedicalRecordNumbers">The candidate's medical record numbers.</param>
public sealed record PersonMatchCandidate(
    int PersonId,
    double Score,
    DateOnly? DateOfBirth,
    IReadOnlyCollection<string> MedicalRecordNumbers);

/// <summary>
/// One side of a person comparison, as plain in-memory data: the incoming
/// person a match is sought for, or a candidate already on file. A blank or
/// absent field is one this side does not know, and a field only one side
/// knows neither adds to the score nor counts against it.
/// </summary>
/// <param name="LastName">The last name.</param>
/// <param name="FirstName">The first name.</param>
/// <param name="BirthDate">The date of birth.</param>
/// <param name="BirthGender">The code for the sex recorded at birth.</param>
/// <param name="Identifiers">Identifier values, medical record numbers among them.</param>
/// <param name="Addresses">
/// Address keys as <see cref="PersonMatchRules.AddressKey"/> builds them.
/// </param>
public sealed record PersonMatchSubject(
    string? LastName,
    string? FirstName,
    DateOnly? BirthDate,
    string? BirthGender,
    IReadOnlyCollection<string> Identifiers,
    IReadOnlyCollection<string> Addresses);

/// <summary>
/// What <see cref="PersonMatchRules.Score"/> makes of a candidate: a score
/// from 0 to 100 and, in words, why.
/// </summary>
/// <param name="Score">The score, from 0 to 100.</param>
/// <param name="Reasons">Each field that agreed or differed, in the order compared.</param>
public sealed record PersonMatchScore(double Score, IReadOnlyList<string> Reasons);

/// <summary>
/// The scores <see cref="PersonMatchRules.Classify"/> divides candidates at:
/// at or above <see cref="Exact"/> is the same person, below
/// <see cref="Probable"/> is not, and between them is a probable match a person
/// confirms. Settable so a deployment can bind them from configuration.
/// </summary>
public sealed record PersonMatchThresholds
{
    /// <summary>At or above this score, a candidate is an exact match.</summary>
    public double Exact { get; init; } = PersonMatchRules.AutoMatchScore;

    /// <summary>Below this score, a candidate is no match.</summary>
    public double Probable { get; init; } = PersonMatchRules.NoMatchScore;
}

/// <summary>
/// The person matching, fuzzy and exact: it scores a candidate against the
/// incoming person and turns the score into one of three outcomes. Pure and
/// over data — it takes its inputs as plain records and reaches for no
/// DbContext, so this module keeps its fence against Survsys.Data and Entity
/// Framework Core.
/// </summary>
/// <remarks>
/// The tiered classification EMSA applies after an EpiTrax people search: a
/// score at the ceiling is the same person, a score below the floor is not,
/// and a score in the fuzzy band between them is a plausible match a reviewer
/// confirms — unless a shared medical record number settles it as the same
/// person, or a missing date of birth marks the candidate too thin to match.
/// <see cref="Score"/> computes that score here from the fields both sides
/// hold, in place of the EpiTrax people search. The secondary phone and email
/// matching and the last-name-change detection are not here.
/// </remarks>
public static class PersonMatchRules
{
    /// <summary>At or above this score, by default, a candidate is an exact match.</summary>
    public const double AutoMatchScore = 95.0;

    /// <summary>Below this score, by default, a candidate is no match.</summary>
    public const double NoMatchScore = 70.0;

    /// <summary>What an agreeing last name adds to the score.</summary>
    public const double LastNameWeight = 30.0;

    /// <summary>What an agreeing first name adds to the score.</summary>
    public const double FirstNameWeight = 20.0;

    /// <summary>What an agreeing date of birth adds to the score.</summary>
    public const double BirthDateWeight = 30.0;

    /// <summary>What an agreeing sex at birth adds to the score.</summary>
    public const double BirthGenderWeight = 5.0;

    /// <summary>What a shared address adds to the score.</summary>
    public const double AddressWeight = 15.0;

    /// <summary>
    /// Score a candidate against the incoming person. Each of last name, first
    /// name, date of birth and sex at birth that both sides hold and that agree
    /// (trimmed, case-insensitively) adds its weight, and a shared address key
    /// adds <see cref="AddressWeight"/>, so every field agreeing scores 100. A
    /// shared identifier adds nothing to the score — it is what
    /// <see cref="Classify"/> settles a probable match by — but is named among
    /// the reasons.
    /// </summary>
    public static PersonMatchScore Score(PersonMatchSubject incoming, PersonMatchSubject candidate)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(candidate);

        var score = 0.0;
        var reasons = new List<string>();

        void Compare(string field, string? left, string? right, double weight)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return;
            }

            if (string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                score += weight;
                reasons.Add($"{field} matches");
            }
            else
            {
                reasons.Add($"{field} differs");
            }
        }

        Compare("Last name", incoming.LastName, candidate.LastName, LastNameWeight);
        Compare("First name", incoming.FirstName, candidate.FirstName, FirstNameWeight);
        Compare("Birth date", incoming.BirthDate?.ToString("yyyy-MM-dd"),
            candidate.BirthDate?.ToString("yyyy-MM-dd"), BirthDateWeight);
        Compare("Sex at birth", incoming.BirthGender, candidate.BirthGender, BirthGenderWeight);

        if (Shared(candidate.Addresses, incoming.Addresses) is not null)
        {
            score += AddressWeight;
            reasons.Add("Address matches");
        }
        else if (Both(candidate.Addresses, incoming.Addresses))
        {
            reasons.Add("Address differs");
        }

        if (Shared(candidate.Identifiers, incoming.Identifiers) is { } identifier)
        {
            reasons.Add($"Shares identifier {identifier}");
        }
        else if (Both(candidate.Identifiers, incoming.Identifiers))
        {
            reasons.Add("Shares no identifier");
        }

        return new PersonMatchScore(score, reasons);
    }

    /// <summary>
    /// The key an address is compared by: the street with its spaces collapsed
    /// and lower-cased, and the first five digits of the zip, joined as
    /// <c>street_zip</c>. Null where the street is blank or the zip has fewer
    /// than five digits, because an address missing either is not one two
    /// people can be said to share.
    /// </summary>
    public static string? AddressKey(string? street, string? postalCode)
    {
        if (string.IsNullOrWhiteSpace(street) || postalCode is null)
        {
            return null;
        }

        var zip = new string(postalCode.Where(char.IsAsciiDigit).Take(5).ToArray());
        if (zip.Length < 5)
        {
            return null;
        }

        var words = street.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return $"{string.Join(' ', words).ToLowerInvariant()}_{zip}";
    }

    /// <summary>
    /// Classify a candidate against the incoming message's medical record
    /// numbers. A score at or above the thresholds' exact score (by default
    /// <see cref="AutoMatchScore"/>) is <see cref="PersonMatchKind.Exact"/>
    /// and a score below their probable score (by default
    /// <see cref="NoMatchScore"/>) is <see cref="PersonMatchKind.None"/>. In the
    /// fuzzy band between them, a medical record number the candidate shares
    /// with the message (compared case-insensitively) promotes it to
    /// <see cref="PersonMatchKind.Exact"/>, a candidate with no date of birth is
    /// <see cref="PersonMatchKind.None"/>, and anything else is
    /// <see cref="PersonMatchKind.Fuzzy"/>.
    /// </summary>
    public static PersonMatchKind Classify(
        PersonMatchCandidate candidate,
        IReadOnlyCollection<string> incomingMedicalRecordNumbers,
        PersonMatchThresholds? thresholds = null)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(incomingMedicalRecordNumbers);

        thresholds ??= new PersonMatchThresholds();

        if (candidate.Score >= thresholds.Exact)
        {
            return PersonMatchKind.Exact;
        }

        if (candidate.Score < thresholds.Probable)
        {
            return PersonMatchKind.None;
        }

        if (Shared(candidate.MedicalRecordNumbers, incomingMedicalRecordNumbers) is not null)
        {
            return PersonMatchKind.Exact;
        }

        return candidate.DateOfBirth is null ? PersonMatchKind.None : PersonMatchKind.Fuzzy;
    }

    /// <summary>
    /// The first of <paramref name="candidate"/>'s values that
    /// <paramref name="incoming"/> also holds, compared case-insensitively, or
    /// null where they share none.
    /// </summary>
    private static bool Both(IReadOnlyCollection<string>? candidate, IReadOnlyCollection<string>? incoming) =>
        candidate is { Count: > 0 } && incoming is { Count: > 0 };

    private static string? Shared(IReadOnlyCollection<string>? candidate, IReadOnlyCollection<string>? incoming)
    {
        if (candidate is null || candidate.Count == 0 || incoming is null || incoming.Count == 0)
        {
            return null;
        }

        var seen = new HashSet<string>(incoming, StringComparer.OrdinalIgnoreCase);
        return candidate.FirstOrDefault(seen.Contains);
    }
}
