namespace Survsys.Contracts;

/// <summary>
/// The privileges a role may hold: a fixed catalogue, because only code can
/// enforce a privilege, and a privilege no code checks would be a promise
/// nothing keeps.
/// </summary>
/// <remarks>
/// Here rather than in the API tier because both tiers read them: the API
/// tier decides what a request needs, and the application tier decides which
/// navigation entries a user is shown. The names are what a role's privileges
/// are stored as and what travel on the wire, so a name is never changed once
/// it is in use.
///
/// The legacy <c>privilege</c> table is a table because the legacy edits it;
/// nothing here does, so there is no table to keep in step with this list.
/// </remarks>
public static class Privileges
{
    /// <summary>Reading case records: people, reports and everything on them.</summary>
    public const string ViewCases = "view_cases";

    /// <summary>Creating and changing case records.</summary>
    public const string EditCases = "edit_cases";

    /// <summary>
    /// Changing the reference data: conditions, agencies, facilities and core
    /// element visibility. Reading it needs no privilege beyond being signed in.
    /// </summary>
    public const string ManageReferenceData = "manage_reference_data";

    /// <summary>Administering users and roles, and reading the access audit.</summary>
    public const string ManageUsers = "manage_users";

    /// <summary>
    /// Running ad-hoc reports: counts of case morbidity reports across every
    /// case, which is why it is a privilege of its own rather than a case read.
    /// </summary>
    public const string RunReports = "run_reports";

    /// <summary>
    /// Routing a case morbidity report to another agency, which starts it at
    /// the assigned-to-LHD state in that agency's queue.
    /// </summary>
    public const string RouteCases = "route_cases";

    /// <summary>Naming the investigator on a case morbidity report.</summary>
    public const string AssignInvestigators = "assign_investigators";

    /// <summary>
    /// The state's review of a closed investigation: approving it, rejecting
    /// it back to the department, and reopening a finished or declined case.
    /// </summary>
    public const string ApproveCases = "approve_cases";

    /// <summary>
    /// Finding duplicate persons and merging one into another, which moves
    /// every case and record from one person to the other.
    /// </summary>
    public const string MergePersons = "merge_persons";

    /// <summary>Every privilege there is, in the order a screen lists them.</summary>
    public static IReadOnlyList<string> All { get; } =
        [ViewCases, EditCases, ManageReferenceData, ManageUsers, RunReports, RouteCases, AssignInvestigators, ApproveCases, MergePersons];
}
