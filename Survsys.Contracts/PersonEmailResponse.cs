namespace Survsys.Contracts;

/// <summary>
/// One email address of a person.
/// </summary>
/// <remarks>
/// <see cref="CurrentEmail"/> is nullable for the reason the stored column is:
/// an address nobody has said anything about is not the same as an address
/// somebody said is no longer used.
/// </remarks>
/// <param name="Id">The email address's identifier.</param>
/// <param name="EmailAddress">The address itself.</param>
/// <param name="CurrentEmail">Whether the person still uses it, where anyone has said.</param>
public record PersonEmailResponse(
    int Id,
    string? EmailAddress,
    bool? CurrentEmail);
