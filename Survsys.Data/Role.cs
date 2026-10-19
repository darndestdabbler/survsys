namespace Survsys.Data;

/// <summary>
/// A named set of privileges a user is given: the legacy <c>role</c> table.
/// </summary>
/// <remarks>
/// What the role may do is its <see cref="RolePrivilege"/> rows. Unlike the
/// legacy <c>user_agency_role</c>, a role is held everywhere rather than in
/// one agency.
/// </remarks>
public class Role
{
    /// <summary>The Administrator role the migration seeds, holding every privilege.</summary>
    public const int AdministratorId = 1;

    public int Id { get; set; }

    /// <summary>The role's name; unique.</summary>
    public string Name { get; set; } = "";
}
