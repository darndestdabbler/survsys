namespace Survsys.Contracts;

/// <summary>
/// Two people on file the matcher takes for one: who they are, how well they
/// match, what that makes them and why.
/// </summary>
/// <param name="First">The one of the two with the lower id.</param>
/// <param name="Second">The other.</param>
/// <param name="Score">The match score, from 0 to 100.</param>
/// <param name="Verdict">
/// <see cref="PersonMatchCandidateResponse.Exact"/> or
/// <see cref="PersonMatchCandidateResponse.Probable"/>.
/// </param>
/// <param name="Reasons">Each field that agreed or differed, in words.</param>
public record DuplicatePersonPairResponse(
    PersonResponse First,
    PersonResponse Second,
    double Score,
    string Verdict,
    IReadOnlyList<string> Reasons);
