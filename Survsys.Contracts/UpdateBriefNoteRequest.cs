namespace Survsys.Contracts;

/// <summary>
/// The report header's brief note: what
/// <c>PUT /api/person-conditions/{id}/brief-note</c> takes.
/// </summary>
/// <param name="BriefNote">The note, at most 500 characters, or null or blank to clear it.</param>
public record UpdateBriefNoteRequest(string? BriefNote);
