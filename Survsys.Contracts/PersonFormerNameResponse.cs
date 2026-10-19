namespace Survsys.Contracts;

/// <summary>
/// A name a person was on file under before it was changed. Recorded by a
/// save that changed the name, and only ever read.
/// </summary>
/// <param name="LastName">The last name as it was.</param>
/// <param name="FirstName">The first name as it was.</param>
/// <param name="MiddleName">The middle name as it was.</param>
/// <param name="ChangedOn">The day the name was changed from this one.</param>
public record PersonFormerNameResponse(
    string LastName,
    string? FirstName,
    string? MiddleName,
    DateOnly ChangedOn);
