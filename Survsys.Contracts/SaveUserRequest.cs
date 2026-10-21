namespace Survsys.Contracts;

/// <summary>
/// A user to create or to save over an existing one: what
/// <c>POST /api/users</c> and <c>PUT /api/users/{id}</c> take.
/// </summary>
/// <remarks>
/// The roles and the agencies are the whole of what the user holds: a save
/// replaces each list.
/// A new user needs a password; saving an existing one changes the password
/// only where one is given.
/// </remarks>
/// <param name="UserName">What the user signs in with. Required, and unique.</param>
/// <param name="DisplayName">The name screens show for the user. Required.</param>
/// <param name="Active">Whether the user may sign in.</param>
/// <param name="RoleIds">The roles the user holds, or null for none.</param>
/// <param name="Password">The user's new password, or null to keep the one they have.</param>
/// <param name="AgencyIds">
/// The agencies the user works for, whose cases they may reach, or null for none.
/// </param>
public record SaveUserRequest(
    string UserName,
    string DisplayName,
    bool Active = true,
    IReadOnlyList<int>? RoleIds = null,
    string? Password = null,
    IReadOnlyList<int>? AgencyIds = null);
