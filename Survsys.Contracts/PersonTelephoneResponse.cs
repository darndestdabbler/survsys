namespace Survsys.Contracts;

/// <summary>
/// One telephone number of a person, as the Telephones section of the
/// Demographic tab shows it.
/// </summary>
/// <remarks>
/// The number stays in its four parts, as the screen takes it and as the
/// legacy data holds it. Nothing here formats them into a number to dial:
/// a screen that wants one string composes it, and the one that wants four
/// boxes does not have to take it apart again.
/// </remarks>
/// <param name="Id">The telephone's identifier.</param>
/// <param name="TelephoneType">What kind of number this is, as the legacy telephone-type code.</param>
/// <param name="CountryCode">The country code.</param>
/// <param name="AreaCode">The area code.</param>
/// <param name="PhoneNumber">The number itself, without its area or country code.</param>
/// <param name="Extension">The extension.</param>
/// <param name="Beginning">The earliest date this is known to have been the person's number.</param>
/// <param name="Note">Whatever the investigator wrote about this number.</param>
public record PersonTelephoneResponse(
    int Id,
    string? TelephoneType,
    string? CountryCode,
    string? AreaCode,
    string? PhoneNumber,
    string? Extension,
    DateOnly? Beginning,
    string? Note);
