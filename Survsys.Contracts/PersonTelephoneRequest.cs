namespace Survsys.Contracts;

/// <summary>
/// One telephone number of a person, as the Demographic tab sends it to be
/// saved.
/// </summary>
/// <remarks>
/// <see cref="PersonTelephoneResponse"/> without its identifier, for the
/// reason <see cref="PersonAddressRequest"/> is without one. The number stays
/// in its four parts, as the screen takes it and as the legacy data holds it.
/// </remarks>
/// <param name="TelephoneType">What kind of number this is, as the legacy telephone-type code.</param>
/// <param name="CountryCode">The country code.</param>
/// <param name="AreaCode">The area code.</param>
/// <param name="PhoneNumber">The number itself, without its area or country code.</param>
/// <param name="Extension">The extension.</param>
/// <param name="Beginning">The earliest date this is known to have been the person's number.</param>
/// <param name="Note">Whatever the investigator wrote about this number.</param>
public record PersonTelephoneRequest(
    string? TelephoneType = null,
    string? CountryCode = null,
    string? AreaCode = null,
    string? PhoneNumber = null,
    string? Extension = null,
    DateOnly? Beginning = null,
    string? Note = null);
