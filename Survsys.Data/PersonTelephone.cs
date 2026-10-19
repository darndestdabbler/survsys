namespace Survsys.Data;

/// <summary>
/// One telephone number of a person.
/// </summary>
/// <remarks>
/// EpiTrax's own <c>person_telephone</c> table, with <c>note</c> and
/// <c>beginning</c> as <c>V0192__add_person_telephone_note.sql</c> added them.
///
/// The number is kept in the legacy system's four parts — country code, area
/// code, number, extension — rather than as one string, because that is how
/// the Telephones section of the Demographic tab takes it and how the legacy
/// data holds it. Nothing here formats them into a number to dial: that is a
/// screen's business.
/// </remarks>
public class PersonTelephone
{
    public int Id { get; set; }

    /// <summary>The person whose telephone this is.</summary>
    public int PersonId { get; set; }

    /// <summary>
    /// What kind of number this is — home, work, mobile — as the legacy
    /// telephone-type code.
    /// </summary>
    public string? TelephoneType { get; set; }

    public string? CountryCode { get; set; }

    public string? AreaCode { get; set; }

    /// <summary>The number itself, without its area or country code.</summary>
    public string? PhoneNumber { get; set; }

    public string? Extension { get; set; }

    /// <summary>
    /// The earliest date this is known to have been the person's number. A
    /// date and not a timestamp, for the reason
    /// <see cref="PersonAddress.Beginning"/> is one.
    /// </summary>
    public DateOnly? Beginning { get; set; }

    /// <summary>Whatever the investigator wrote about this number.</summary>
    public string? Note { get; set; }
}
