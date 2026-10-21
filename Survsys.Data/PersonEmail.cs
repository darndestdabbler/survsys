namespace Survsys.Data;

/// <summary>
/// One email address of a person.
/// </summary>
/// <remarks>
/// EpiTrax's own <c>person_email</c> table. <see cref="CurrentEmail"/> is
/// nullable because the legacy column is: a row written before anyone said
/// whether the address is still in use says nothing either way, and that is
/// not the same as saying no.
/// </remarks>
public class PersonEmail
{
    public int Id { get; set; }

    /// <summary>The person whose email address this is.</summary>
    public int PersonId { get; set; }

    public string? EmailAddress { get; set; }

    /// <summary>
    /// Whether this is an address the person still uses, where anyone has
    /// said.
    /// </summary>
    public bool? CurrentEmail { get; set; }
}
