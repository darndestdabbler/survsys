using Microsoft.Data.SqlClient;

namespace Survsys.Uat;

/// <summary>
/// The rows a walkthrough starts from: what is cleared, in what order, and
/// which fixtures are run after. The one copy of it, called by the end-to-end
/// harness (<c>Survsys.E2ETests/SurvsysHarness.cs</c>) and by this kit's
/// <c>reset</c>, so a database a person performs the walkthroughs against is
/// the one their tests are performed against.
/// </summary>
public static class WalkthroughDatabase
{
    /// <summary>
    /// The databases the end-to-end harness runs against by default. The kit
    /// never recreates either, because a person resetting theirs would throw
    /// away a walkthrough run in progress.
    /// </summary>
    public static readonly string[] HarnessDatabases = ["survsys_e2e", "survsys_e2e_intake"];

    /// <summary>
    /// The Administrator role's id, as the migration seeds it; stated here
    /// because this project may not reference <c>Survsys.Data</c>.
    /// </summary>
    public const int AdministratorRoleId = 1;

    /// <summary>
    /// The fixtures the main harness seeds, in the order they run: the
    /// conditions first, because a report names one of them, then the people,
    /// then the staged messages the ELR walkthroughs act on — which name
    /// neither a person nor a condition the schema enforces — and last the
    /// notification guide, which names a condition the first has seeded.
    /// </summary>
    public static readonly string[] MainFixtures =
        ["conditions.sql", "persons.sql", "staged-messages.sql", "notification-guide.sql"];

    /// <summary>
    /// The fixtures the intake harness seeds, in the order they run: the
    /// conditions and the people, then the reference data the ELR and eCR
    /// intakes translate by, and no staged message or eCR document — what is
    /// in those queues is what a walkthrough dropped.
    /// </summary>
    public static readonly string[] IntakeFixtures =
        ["conditions.sql", "persons.sql", "elr-intake.sql", "ecr-intake.sql"];

    /// <summary>
    /// The tables the fixtures fill, and the ones that hang off them, in the
    /// order they can be emptied: each table before the one it references,
    /// so the laboratory rows go before the reports and the people they
    /// name, the reports before the people, and the people before the
    /// conditions. Cleared before any insert, so a second run of the suite
    /// seeds the same seven people, seven conditions and one guide rather than
    /// fourteen of each — and no report, export, queued notification, specimen, exposure,
    /// note, task, attachment, facility, saved view, form, vaccine record,
    /// outbreak or treatment at all, since a
    /// walkthrough that needs one creates it, and one left over from the last
    /// run would be one the document did not describe.
    /// </summary>
    /// <remarks>
    /// The laboratory chain is listed in full rather than left to the
    /// database's cascades: a specimen names its person with a reference the
    /// schema refuses to cascade, so a specimen left behind would refuse the
    /// clearing of the people, and the run after a laboratory walkthrough
    /// would fail to seed. Results go before tests, tests and susceptibility
    /// rows before specimens, the links and the shares before the reports
    /// they hang off, and specimens before the people they name.
    /// <para>
    /// The CDC export log (<c>nmi_log</c>) and the notification queue
    /// (<c>nmi_export_queue</c>) both name their report with a reference the
    /// schema cascades, so the reports' clearing would take them anyway; they
    /// are listed before <c>person_conditions</c> so the order says what it
    /// clears rather than leaving it to the cascade. The queue is filled by
    /// the walkthroughs that queue a case, and the log by the queue worker when
    /// they press Process now.
    /// </para>
    /// <para>
    /// The notification guides go after the people and before the conditions:
    /// <c>Fixtures/notification-guide.sql</c> reseeds the one the CDC
    /// walkthroughs send under, and the guide walkthrough adds one of its own.
    /// A guide's condition, variables, links and header values name it by bare
    /// ids or by references the schema refuses to cascade, so they go first.
    /// The PHIN variable catalogue follows the guides that carry its variables:
    /// no migration seeds it, and the guide walkthrough adds the variable it
    /// gives its guide, which a second run could not add again.
    /// </para>
    /// <para>
    /// The exposure chain is listed in full for the same reason, and it is
    /// the one phase 3 brought that matters most: an exposure contact names
    /// its person with a reference the schema refuses to cascade, so a
    /// contact left behind would refuse the clearing of the people and the
    /// run after a contacts walkthrough would fail to seed. The contacts and
    /// the sources go before the exposure they hang off, and the exposure
    /// before the place it names by <c>exposure_place_id</c>. An attachment
    /// goes before the <c>attachment_files</c> row holding its bytes, which
    /// is the row it names. The notes and the tasks name nothing the schema
    /// enforces, and are listed above the reports they are raised against
    /// because that is where they belong in this order. A facility names its
    /// agency by a reference the schema enforces, but the agencies are the
    /// migration's and the suite never clears them, so a facility is cleared
    /// on its own and listed first, where nothing the suite clears hangs off
    /// it. A saved view is cleared the same way and for the same reason: it
    /// names its user by a reference the schema enforces, but the API tier
    /// creates the administrator and the suite never clears that user;
    /// <see cref="AccountsClearedButTheAdministrator"/> clears the others.
    /// </para>
    /// <para>
    /// Phase 7's tables are listed for the reason the laboratory chain is:
    /// each names something the suite clears by a reference the schema
    /// refuses to cascade. A form, an outbreak and a treatment's condition
    /// list each name their condition that way, so one left behind would
    /// refuse the clearing of the conditions; an answer names its question
    /// and an assignment its form the same way, so the answers and the
    /// assignments go before the form tree, and the tree is listed leaf
    /// first, options, questions and sections before the form. A vaccine
    /// record and a form assignment hang off their report and go before it.
    /// The outbreaks go after the reports, because a report names its
    /// outbreak, with the outbreak's access, report sections and activity
    /// before it; the treatments go after their condition lists. The vaccines
    /// catalogue and the report sections are the migration's and are kept,
    /// as the agencies are.
    /// </para>
    /// <para>
    /// The staged messages phase 5 brought are listed last, after the
    /// conditions and the people: <c>system_messages</c> names neither a
    /// person nor a condition the schema enforces and nothing hangs off it, so
    /// its place in this order is free, and last is where it sits with the
    /// other tables the fixtures fill and keep. It is cleared for a second
    /// reason the conditions and the people are not — a promoted message is
    /// moved to the assigned state in place, so a run after a promotion
    /// walkthrough would start from a queue that is already a case rather than
    /// from the seed, and the clearing is what puts it back.
    /// </para>
    /// <para>
    /// The vocabulary, its rules and the zip-to-agency mapping phase 10's
    /// intake reads go first: a lab's own codes name their facility, and the
    /// master LOINC rows and the rules name their condition, by references the
    /// schema refuses to cascade. <c>Fixtures/elr-intake.sql</c> fills them for
    /// the intake harness; for the main harness they start empty, and the
    /// LOINC walkthrough adds what it needs.
    /// </para>
    /// <para>
    /// Phase 13's eCR tables go before everything: a document's findings, RR
    /// conditions and case links hang off it, a case link names its report
    /// and a condition mapping its condition, by references the schema
    /// refuses to cascade. <c>Fixtures/ecr-intake.sql</c> fills the mapping
    /// for the intake harness, and what the eCR walkthroughs drop
    /// fills the rest; a document left from the last run would be one its
    /// walkthrough could not take in again, its id already on file.
    /// </para>
    /// </remarks>
    public static readonly string[] TablesInDeletionOrder =
    [
        "ecr_case_value",
        "ecr_case_link",
        "ecr_validation_finding",
        "ecr_rr_condition",
        "ecr_document",
        "ecr_condition_mapping",
        "ecr_sender",
        "vocab_rule_conditions",
        "vocab_rules",
        "vocab_child_vocab",
        "vocab_child_loinc",
        "vocab_master_loinc",
        "vocab_master_vocab",
        "vocab_master_organism",
        "zip_code_agencies",
        "facilities",
        "saved_views",
        "attachments",
        "attachment_files",
        "notes",
        "tasks",
        "exposure_contacts",
        "exposure_sources",
        "exposures",
        "exposure_places",
        "person_condition_shares",
        "person_condition_labs",
        "lab_test_results",
        "lab_tests",
        "lab_resist_tests",
        "labs",
        "nmi_log",
        "nmi_export_queue",
        "form_answers",
        "form_assignments",
        "form_question_options",
        "form_questions",
        "form_sections",
        "forms",
        "person_vaccines",
        "person_treatments",
        "encounters",
        "person_conditions",
        "outbreak_access",
        "outbreak_report_sections",
        "outbreak_updates",
        "outbreaks",
        "condition_treatments",
        "treatments",
        "parent_guardians",
        "person_races",
        "person_aliases",
        "person_emails",
        "person_telephones",
        "person_addresses",
        "persons",
        "condition_detail",
        "guide_phin_var",
        "guide_link",
        "nmi_guide_variable",
        "guide",
        "phin_var",
        "conditions",
        "system_messages",
    ];

    /// <summary>
    /// The users and roles a walkthrough adds, cleared after
    /// <see cref="TablesInDeletionOrder"/> and before any insert: every user
    /// but <paramref name="administratorUserName"/> and every role but the
    /// Administrator role, with what names them. They are not emptied, as
    /// those tables are, because the API tier creates the administrator and
    /// the migration its role, and a walkthrough signs in as that user.
    /// </summary>
    /// <remarks>
    /// A user's access records name them by a reference the schema refuses to
    /// cascade, so they go first; the reports, saved views and outbreak rows
    /// that name a user are already gone with the tables above.
    /// </remarks>
    public static string[] AccountsClearedButTheAdministrator(string administratorUserName)
    {
        var administrator = administratorUserName.Replace("'", "''", StringComparison.Ordinal);

        return
        [
            $"DELETE FROM access_records WHERE user_id IN (SELECT id FROM users WHERE user_name <> N'{administrator}')",
            $"DELETE FROM user_agencies WHERE user_id IN (SELECT id FROM users WHERE user_name <> N'{administrator}')",
            $"DELETE FROM user_roles WHERE role_id <> {AdministratorRoleId} OR user_id IN (SELECT id FROM users WHERE user_name <> N'{administrator}')",
            $"DELETE FROM users WHERE user_name <> N'{administrator}'",
            $"DELETE FROM role_privileges WHERE role_id <> {AdministratorRoleId}",
            $"DELETE FROM roles WHERE id <> {AdministratorRoleId}",
        ];
    }

    /// <summary>
    /// Clears what <see cref="TablesInDeletionOrder"/> and
    /// <see cref="AccountsClearedButTheAdministrator"/> name, then runs
    /// <paramref name="fixtures"/> from <paramref name="fixturesDirectory"/>
    /// in their order, into a database already at this build's migration
    /// level. A missing fixture is refused before anything is cleared; a
    /// server that cannot be reached leaves its <see cref="SqlException"/> to
    /// the caller, which knows what to tell its reader.
    /// </summary>
    public static void Seed(
        string connectionString, string fixturesDirectory, IEnumerable<string> fixtures, string administratorUserName)
    {
        var paths = fixtures.Select(fixture => Path.Combine(fixturesDirectory, fixture)).ToList();

        foreach (var path in paths.Where(path => !File.Exists(path)))
        {
            throw new InvalidOperationException(
                $"The fixture is not at {path}. The fixtures are copied into the build output "
                + "of Survsys.E2ETests and of Survsys.Uat, most of them linked from "
                + "Survsys.IntegrationTests; without one the screen it fills would have no rows "
                + "and a walkthrough would read an empty table.");
        }

        using var connection = new SqlConnection(connectionString);
        connection.Open();

        var statements = TablesInDeletionOrder.Select(table => $"DELETE FROM {table}")
            .Concat(AccountsClearedButTheAdministrator(administratorUserName))
            .Concat(paths.Select(File.ReadAllText));

        foreach (var statement in statements)
        {
            using var command = connection.CreateCommand();
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Drops the database <paramref name="connectionString"/> names, if it is
    /// there, and creates it empty, for <c>Survsys.Api</c> to migrate. Refuses
    /// <see cref="HarnessDatabases"/>.
    /// </summary>
    public static void Recreate(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var database = builder.InitialCatalog;

        if (database.Length == 0
            || HarnessDatabases.Contains(database, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The kit will not recreate {(database.Length == 0 ? "a connection string naming no database" : database)}. "
                + $"{string.Join(" and ", HarnessDatabases)} are the end-to-end harness's; name a "
                + "database of the kit's own, as its default survsys_uat is.");
        }

        builder.InitialCatalog = "master";

        using var connection = new SqlConnection(builder.ConnectionString);
        connection.Open();

        var quoted = "[" + database.Replace("]", "]]", StringComparison.Ordinal) + "]";
        var literal = database.Replace("'", "''", StringComparison.Ordinal);

        using var command = connection.CreateCommand();
        command.CommandText =
            $"IF DB_ID(N'{literal}') IS NOT NULL "
            + $"BEGIN ALTER DATABASE {quoted} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {quoted}; END; "
            + $"CREATE DATABASE {quoted};";
        command.ExecuteNonQuery();

        // A pooled connection to the dropped database would fail the first
        // command that reused it.
        SqlConnection.ClearAllPools();
    }
}
