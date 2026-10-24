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
/// users, roles, the privilege catalogue, the access and change audits and the
/// system settings need
/// <see cref="Privileges.ManageUsers"/>; the reference data — conditions,
/// agencies, facilities, core elements, the code lists, the ELR vocabulary,
/// the Connecticut towns, the zip codes, the vaccine catalogue, the form and
/// treatment catalogues and the
/// notification setup (guides, PHIN variables, rule sets, value sets and
/// hepatitis lab groups) — is readable by anyone signed in,
/// because every case screen reads it, and changed with
/// <see cref="Privileges.ManageReferenceData"/>; who the caller is
/// (<c>/api/me</c>) needs nothing beyond being signed in; a saved view and its
/// export need <see cref="Privileges.ViewCases"/> whatever the method, because
/// a view is the caller's own and changes no case; an ad-hoc report needs
/// <see cref="Privileges.RunReports"/> whatever the method; duplicate persons
/// and their merges (<c>/api/person-merges</c>) need
/// <see cref="Privileges.MergePersons"/> whatever the method; a workflow move
/// (<c>PUT /api/person-conditions/{id}/workflow</c>) needs only
/// <see cref="Privileges.ViewCases"/> here, because the endpoint itself asks
/// the privilege the particular transition needs — a state approver need not
/// hold <see cref="Privileges.EditCases"/> to approve; and every other API
/// request is a case record, read with <see cref="Privileges.ViewCases"/> and
/// written with <see cref="Privileges.EditCases"/>.
/// </remarks>
public static class PrivilegeRules
{
    /// <summary>The claim type a signed-in caller's privileges travel as.</summary>
    public const string PrivilegeClaim = "privilege";

    private static readonly string[] Administration =
        ["/api/users", "/api/roles", "/api/privileges", "/api/access-audit", "/api/audit", "/api/system-settings"];

    private static readonly string[] ReferenceData =
    [
        "/api/conditions", "/api/agencies", "/api/facilities", "/api/core-elements", "/api/codes", "/api/vocabulary",
        "/api/ct-towns", "/api/zip-codes", "/api/vaccines", "/api/notification-setup", "/api/forms", "/api/treatments",
        "/api/ecr-condition-mappings", "/api/ecr-senders",
    ];

    private static readonly string[] AnySignedIn = ["/api/me"];

    private static readonly string[] CaseReadsWhateverTheMethod = ["/api/saved-views"];

    private static readonly string[] Reports = ["/api/reports"];

    private static readonly string[] PersonMerges = ["/api/person-merges"];

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

        if (Under(path, Reports))
        {
            return Privileges.RunReports;
        }

        if (Under(path, PersonMerges))
        {
            return Privileges.MergePersons;
        }

        if (AWorkflowMove(path))
        {
            return Privileges.ViewCases;
        }

        return reads ? Privileges.ViewCases : Privileges.EditCases;
    }

    /// <summary>
    /// Whether the path is a report's workflow resource, whose write the
    /// endpoint gates by the transition asked for.
    /// </summary>
    private static bool AWorkflowMove(string path) =>
        Under(path, ["/api/person-conditions"])
            && path.EndsWith("/workflow", StringComparison.OrdinalIgnoreCase);

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
