namespace Survsys.Contracts;

/// <summary>
/// A user, as <c>/api/users</c> returns one. Never the password or its hash.
/// </summary>
/// <param name="Id">The user's identifier.</param>
/// <param name="UserName">What the user signs in with.</param>
/// <param name="DisplayName">The name screens show for the user.</param>
/// <param name="Active">Whether the user may sign in.</param>
/// <param name="RoleIds">The roles the user holds, as <c>/api/roles</c> identifies them.</param>
public record UserResponse(
    int Id,
    string UserName,
    string DisplayName,
    bool Active,
    IReadOnlyList<int> RoleIds);
