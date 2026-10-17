namespace Survsys.Contracts;

/// <summary>A role, as <c>/api/roles</c> returns one.</summary>
/// <param name="Id">The role's identifier.</param>
/// <param name="Name">The role's name.</param>
/// <param name="Privileges">The privileges the role holds, from <see cref="Contracts.Privileges"/>.</param>
public record RoleResponse(
    int Id,
    string Name,
    IReadOnlyList<string> Privileges);
