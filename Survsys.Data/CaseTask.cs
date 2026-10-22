namespace Survsys.Data;

/// <summary>
/// A task: one thing somebody has to do about a case morbidity report, with
/// when it is due, what state it is in, and — where it repeats — the series it
/// belongs to.
/// </summary>
/// <remarks>
/// <strong>Named <c>CaseTask</c> and not <c>Task</c> on purpose.</strong> An
/// entity type called <c>Task</c> would shadow
/// <see cref="System.Threading.Tasks.Task"/> in every file of this assembly
/// that awaits anything, which is most of them, and the next reader would be
/// left wondering why <c>Task&lt;int&gt;</c> no longer compiles. The table
/// keeps the legacy name.
///
/// The columns are EpiTrax's own <c>task</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, down to their snake_case names. The screen is the
/// Notes tab's Tasks section — "Task Schedule", "+ Add Task" and a "Show all
/// tasks" filter — as
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps it, and the task list screen that shows the same tasks across reports.
///
/// Two legacy columns are not here. <c>creator_user_id</c> would say who
/// raised the task where <see cref="UserId"/> says whose it is, and
/// <c>task_tracking_key</c> is a legacy internal correlation key: nothing
/// reads either and no screen shows either, so neither is written for one
/// release — the reason <see cref="Exposure"/> omits the legacy
/// <c>outbreak_id</c>.
///
/// A repeating task is expanded into its dated tasks when it is recorded, each
/// later one pointing at the first through <see cref="RepeatingTaskId"/>;
/// nothing afterwards re-expands a series, extends it or cancels the rest of
/// it, and nothing runs on a schedule — a task becomes due by its
/// <see cref="DueDate"/> being read. What counts as a schedule and what dates
/// it expands to is <c>RecurringTaskRules</c>'s.
///
/// The report, the user and the category are bare nullable ids and not
/// navigations, exactly as <see cref="Note"/>'s and <see cref="Exposure"/>'s
/// are: there is no <c>user</c> table in this schema and the legacy
/// <c>task_category</c> table is not modelled. There is no behaviour here and
/// no navigation out.
/// </remarks>
public class CaseTask
{
    public int Id { get; set; }

    /// <summary>
    /// What the task is called, as free text — left unbounded, because it
    /// holds whatever was typed into "+ Add Task".
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// What else there is to say about the task, as the legacy <c>notes</c>
    /// column — free text, left unbounded, as <see cref="Name"/> is.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// How urgent the task is, as a code; the legacy column is a plain
    /// <c>VARCHAR</c> and no reference table behind it exists here.
    /// </summary>
    public string? Priority { get; set; }

    /// <summary>
    /// The case morbidity report this task is against, as the legacy
    /// <c>person_condition_id</c>; null where none is set. A bare nullable int
    /// and not a navigation, as <see cref="Note.PersonConditionId"/> is, set
    /// from the report in the route the task is recorded through.
    /// </summary>
    public int? PersonConditionId { get; set; }

    /// <summary>
    /// Whose task it is, as the legacy <c>user_id</c>. A bare nullable int
    /// where the legacy column is <c>NOT NULL</c>, as
    /// <see cref="Note.UserId"/> is: there is no <c>user</c> table in this
    /// schema to point at, so a task may be raised without naming anyone.
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// The day the task is due, where one is set — the date the task list is
    /// ordered by. A <see cref="DateOnly"/> for the legacy <c>DATE</c>.
    /// </summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>When the task was raised, as the legacy <c>created_at</c>.</summary>
    /// <remarks>
    /// A required <see cref="DateTimeOffset"/>, for the reason
    /// <see cref="Note.CreatedAt"/> is one, and set where the task is
    /// recorded.
    /// </remarks>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// When the task was last changed, as the legacy <c>updated_at</c>; null
    /// until something changes it.
    /// </summary>
    /// <remarks>
    /// Set where <see cref="Status"/> is changed, which is the one mutation a
    /// stored task takes. Unlike <see cref="CreatedAt"/> it is nullable,
    /// because a task nothing has touched yet has no such moment.
    /// </remarks>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// What kind of task it is, as the legacy <c>category_id</c>. A bare
    /// nullable id into the legacy <c>task_category</c> reference table, which
    /// is not modelled here.
    /// </summary>
    public int? CategoryId { get; set; }

    /// <summary>
    /// What state the task is in — whether it is still to do or done — as a
    /// code; the legacy column is a plain <c>VARCHAR</c>.
    /// </summary>
    /// <remarks>
    /// A plain code string with no reference table behind it, as
    /// <see cref="Note.NoteType"/> is, and what an offered value may be is the
    /// API tier's to say. The one mutation a stored task takes.
    /// </remarks>
    public string? Status { get; set; }

    /// <summary>
    /// The first task of the series this task belongs to, as the legacy
    /// <c>repeating_task_id</c>; null on a task that does not repeat and on
    /// the first task of a series, which is the one the rest point at.
    /// </summary>
    /// <remarks>
    /// A bare nullable int into this same table and not a navigation: the
    /// tasks of a series are read by composing a query on it rather than by
    /// walking a self-reference the entity does not have.
    /// </remarks>
    public int? RepeatingTaskId { get; set; }

    /// <summary>
    /// The last day a repeating task's series may fall on, as the legacy
    /// <c>until_date</c>; null on a task that does not repeat. A
    /// <see cref="DateOnly"/> for the legacy <c>DATE</c>.
    /// </summary>
    public DateOnly? UntilDate { get; set; }

    /// <summary>
    /// How often the task repeats, as the legacy <c>repeating_interval</c>;
    /// null or empty on a task that does not repeat.
    /// </summary>
    /// <remarks>
    /// A plain code string, read through <c>RecurringTaskRules.Read</c>
    /// — which is where the vocabulary and the schedule live, so that what an
    /// interval means is in one place and not in each caller.
    /// </remarks>
    public string? RepeatingInterval { get; set; }
}
