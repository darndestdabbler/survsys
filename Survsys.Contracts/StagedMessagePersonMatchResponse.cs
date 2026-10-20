namespace Survsys.Contracts;

/// <summary>
/// The people on file who may be a staged message's patient: the patient as
/// the message names them, and the candidates best first.
/// </summary>
/// <remarks>
/// The patient is what a reviewer creates a new person from when no candidate
/// is them. Null, with no candidates, where the message could not be parsed.
/// </remarks>
/// <param name="Patient">The message's patient, or null where it could not be parsed.</param>
/// <param name="Candidates">The candidates, best first.</param>
public record StagedMessagePersonMatchResponse(
    CreatePersonRequest? Patient,
    IReadOnlyList<PersonMatchCandidateResponse> Candidates);
