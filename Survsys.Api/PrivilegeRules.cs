using Survsys.Contracts;

namespace Survsys.Api;

/// <summary>
/// Which privilege a request to this tier needs, decided from its method and
/// path alone.
/// </summary>
/// <remarks>
/// One rule over every route rather than a privilege written beside each
/// endpoint, so an endpoint mapped later is covered the day it is mapped:
/// <c>Program.cs</c> makes this the fallback authorisation policy, which every
/// endpoint that does not say otherwise is held to. Only signing in and the
/// readiness probe say otherwise.
///
/// The rule, in the order it is asked:
/// users, roles, the privilege catalogue and the access audit need
/// <see cref="Privileges.ManageUsers"/>; the reference data — conditions,
/// agencies, facilities and core elements — is readable by anyone signed in,
/// because every case screen reads it, and changed with
/// <see cref="Privileges.ManageReferenceData"/>; who the caller is
/// (<c>/api/me</c>) needs nothing beyond being signed in; a saved view and its
/// export need <see cref="Privileges.ViewCases"/> whatever the method, because
/// a view is the caller's own and changes no case; and every other API
/// request is a case record, read with <see cref="Privileges.ViewCases"/> and
/// written with <see cref="Privileges.EditCases"/>.
/// </remarks>
public static class PrivilegeRules
{
    /// <summary>The claim type a signed-in caller's privileges travel as.</summary>
    public const string PrivilegeClaim = "privilege";

    private static readonly string[] Administration =
        ["/api/users", "/api/roles", "/api/privileges", "/api/access-audit"];

    private static readonly string[] ReferenceData =
        ["/api/conditions", "/api/agencies", "/api/facilities", "/api/core-elements"];

    private static readonly string[] AnySignedIn = ["/api/me"];

    private static readonly string[] CaseReadsWhateverTheMethod = ["/api/saved-views"];

    /// <summary>
    /// The privilege a request needs, or null where being signed in is enough.
    /// </summary>
    public static string? RequiredFor(string method, string path)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(path);

        var reads = HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);

        if (Under(path, Administration))
        {
            return Privileges.ManageUsers;
        }

        if (Under(path, ReferenceData))
        {
            return reads ? null : Privileges.ManageReferenceData;
        }

        if (Under(path, AnySignedIn) || !Under(path, ["/api"]))
        {
            return null;
        }

        if (Under(path, CaseReadsWhateverTheMethod))
        {
            return Privileges.ViewCases;
        }

        return reads ? Privileges.ViewCases : Privileges.EditCases;
    }

    /// <summary>
    /// Whether a caller holding these privileges may make this request.
    /// </summary>
    public static bool Allows(IEnumerable<string> held, string method, string path)
    {
        ArgumentNullException.ThrowIfNull(held);

        return RequiredFor(method, path) is not { } required || held.Contains(required, StringComparer.Ordinal);
    }

    /// <summary>
    /// Whether the path is one of these or below one, by whole segments:
    /// <c>/api/users/3</c> is under <c>/api/users</c>, <c>/api/usersx</c> is not.
    /// </summary>
    private static bool Under(string path, string[] roots) =>
        roots.Any(root => path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            && (path.Length == root.Length || path[root.Length] == '/'));
}
