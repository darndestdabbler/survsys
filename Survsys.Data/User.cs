namespace Survsys.Data;

/// <summary>
/// Someone who signs in to Survsys: the legacy <c>user</c> table, reduced to
/// what signing in and being audited need.
/// </summary>
/// <remarks>
/// The password is never stored, only the hash the API tier's password hasher
/// makes of it, which carries its own algorithm and salt. A user is
/// deactivated rather than deleted, because the access records name them and
/// must go on naming them.
/// </remarks>
public class User
{
    public int Id { get; set; }

    /// <summary>What the user signs in with; unique.</summary>
    public string UserName { get; set; } = "";

    /// <summary>The name screens and the access audit show for the user.</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>The hash of the user's password, never the password.</summary>
    public string PasswordHash { get; set; } = "";

    /// <summary>Whether the user may sign in.</summary>
    public bool Active { get; set; } = true;
}
