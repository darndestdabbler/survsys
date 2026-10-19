namespace Survsys.Contracts;

/// <summary>
/// Who the caller is signed in as, and what they may do: what
/// <c>GET /api/me</c> returns.
/// </summary>
/// <remarks>
/// The privileges are the ones the caller signed in with, so a change to their
/// roles shows here at their next sign-in.
/// </remarks>
/// <param name="Id">The user's identifier.</param>
/// <param name="UserName">What the user signs in with.</param>
/// <param name="DisplayName">The name screens show for the user.</param>
/// <param name="Privileges">The privileges the user's roles hold, from <see cref="Contracts.Privileges"/>.</param>
public record CurrentUserResponse(
    int Id,
    string UserName,
    string DisplayName,
    IReadOnlyList<string> Privileges);
