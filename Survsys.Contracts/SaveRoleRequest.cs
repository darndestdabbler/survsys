namespace Survsys.Contracts;

/// <summary>
/// A role to create or to save over an existing one: what
/// <c>POST /api/roles</c> and <c>PUT /api/roles/{id}</c> take.
/// </summary>
/// <remarks>
/// The privileges are the whole of what the role holds: a save replaces the
/// list. Each is a name from <see cref="Contracts.Privileges"/>.
/// </remarks>
/// <param name="Name">The role's name. Required, and unique.</param>
/// <param name="Privileges">The privileges the role holds, or null for none.</param>
public record SaveRoleRequest(
    string Name,
    IReadOnlyList<string>? Privileges = null);
