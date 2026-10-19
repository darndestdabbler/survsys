namespace Survsys.Contracts;

/// <summary>
/// A person on file scored against the person a match was sought for: who
/// they are, how well they match, what that makes them and why.
/// </summary>
/// <param name="Person">The candidate, as a list of people shows them.</param>
/// <param name="Score">The match score, from 0 to 100.</param>
/// <param name="Verdict">
/// <see cref="Exact"/>, <see cref="Probable"/> or <see cref="None"/>.
/// </param>
/// <param name="Reasons">Each field that agreed or differed, in words.</param>
public record PersonMatchCandidateResponse(
    PersonResponse Person,
    double Score,
    string Verdict,
    IReadOnlyList<string> Reasons)
{
    /// <summary>The same person.</summary>
    public const string Exact = "Exact";

    /// <summary>A plausible match a person must confirm.</summary>
    public const string Probable = "Probable";

    /// <summary>Not a match.</summary>
    public const string None = "None";
}
