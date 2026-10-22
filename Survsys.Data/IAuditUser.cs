namespace Survsys.Data;

/// <summary>
/// Who is making the changes a context saves, for the <see cref="AuditInterceptor"/>
/// to record. The API tier supplies the signed-in caller; a context built
/// without one (a tool, a test) audits its changes with no user.
/// </summary>
public interface IAuditUser
{
    /// <summary>The user's id, or null where nobody is signed in.</summary>
    int? UserId { get; }
}
