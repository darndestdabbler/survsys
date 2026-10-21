namespace Survsys.Contracts;

/// <summary>
/// One email address of a person, as the Demographic tab sends it to be
/// saved.
/// </summary>
/// <remarks>
/// <see cref="PersonEmailResponse"/> without its identifier, for the reason
/// <see cref="PersonAddressRequest"/> is without one. <see cref="CurrentEmail"/>
/// is nullable for the reason the stored column is: an address nobody has said
/// anything about is not the same as one somebody said is no longer used.
/// </remarks>
/// <param name="EmailAddress">The address itself.</param>
/// <param name="CurrentEmail">Whether the person still uses it, where anyone has said.</param>
public record PersonEmailRequest(
    string? EmailAddress = null,
    bool? CurrentEmail = null);
