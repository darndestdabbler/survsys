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
/// The person matching, fuzzy and exact: it turns a search candidate's EpiTrax
/// match score into one of three outcomes. Pure and over data — it takes its
/// inputs as plain records and reaches for no DbContext, so this module keeps
/// its fence against Survsys.Data and Entity Framework Core.
/// </summary>
/// <remarks>
/// The tiered classification EMSA applies after an EpiTrax people search: a
/// score at the ceiling is the same person, a score below the floor is not,
/// and a score in the fuzzy band between them is a plausible match a reviewer
/// confirms — unless a shared medical record number settles it as the same
/// person, or a missing date of birth marks the candidate too thin to match.
/// The score's secondary contact-info matching (phone, email, geocoded
/// address) and the last-name-change detection are not here.
/// </remarks>
public static class PersonMatchRules
{
    /// <summary>At or above this score, a candidate is an exact match.</summary>
    public const double AutoMatchScore = 95.0;

    /// <summary>Below this score, a candidate is no match.</summary>
    public const double NoMatchScore = 70.0;

    /// <summary>
    /// Classify a candidate against the incoming message's medical record
    /// numbers. A score at or above <see cref="AutoMatchScore"/> is
    /// <see cref="PersonMatchKind.Exact"/> and a score below
    /// <see cref="NoMatchScore"/> is <see cref="PersonMatchKind.None"/>. In the
    /// fuzzy band between them, a medical record number the candidate shares
    /// with the message (compared case-insensitively) promotes it to
    /// <see cref="PersonMatchKind.Exact"/>, a candidate with no date of birth is
    /// <see cref="PersonMatchKind.None"/>, and anything else is
    /// <see cref="PersonMatchKind.Fuzzy"/>.
    /// </summary>
    public static PersonMatchKind Classify(
        PersonMatchCandidate candidate, IReadOnlyCollection<string> incomingMedicalRecordNumbers)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(incomingMedicalRecordNumbers);

        if (candidate.Score >= AutoMatchScore)
        {
            return PersonMatchKind.Exact;
        }

        if (candidate.Score < NoMatchScore)
        {
            return PersonMatchKind.None;
        }

        if (SharesMedicalRecordNumber(candidate.MedicalRecordNumbers, incomingMedicalRecordNumbers))
        {
            return PersonMatchKind.Exact;
        }

        return candidate.DateOfBirth is null ? PersonMatchKind.None : PersonMatchKind.Fuzzy;
    }

    private static bool SharesMedicalRecordNumber(
        IReadOnlyCollection<string>? candidate, IReadOnlyCollection<string> incoming)
    {
        if (candidate is null || candidate.Count == 0 || incoming.Count == 0)
        {
            return false;
        }

        var seen = new HashSet<string>(incoming, StringComparer.OrdinalIgnoreCase);
        return candidate.Any(seen.Contains);
    }
}
