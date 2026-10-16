using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the task endpoints make of a stored task, and what they refuse before
/// asking the database anything.
/// </summary>
/// <remarks>
/// No database and no host: that a task records, that a repeating one is
/// written as its whole series in one transaction, that the two lists come
/// back by due date and that a missing report or task is a 404 are the
/// queries' to answer against a real SQL Server, which no session-012
/// snapshot seeds a task row at all. The values here are made up in the shape
/// the legacy <c>task</c> table takes: a weekly follow-up call due in the
/// second week of March.
///
/// What a schedule means is <c>RecurringTaskRulesTests</c>'s, named after the
/// rule that decides it. What is here is that this slice asks that rule and
/// hands back its sentence rather than restating the schedule in a second
/// place.
/// </remarks>
public class TaskEndpointsTests
{
    private static readonly DateTimeOffset Raised =
        new(2026, 3, 13, 0, 13, 17, TimeSpan.FromHours(-4));

    private static readonly DateOnly Due = new(2026, 3, 16);

    private static CaseTask Recorded() => new()
    {
        Id = 21,
        PersonConditionId = 14,
        Name = "Follow-up call",
        Notes = "Ask about the household contacts",
        Priority = "high",
        DueDate = Due,
        UserId = 11,
        CategoryId = 3,
        Status = TaskEndpoints.OpenStatus,
        RepeatingTaskId = 20,
        RepeatingInterval = RecurringTaskRules.WeeklyCode,
        UntilDate = new DateOnly(2026, 4, 13),
        CreatedAt = Raised,
        UpdatedAt = null,
    };

    private static CreateTaskRequest Asked(
        string? name = "Follow-up call",
        string? status = null,
        string? repeatingInterval = null,
        DateOnly? dueDate = null,
        DateOnly? untilDate = null) =>
        new(
            Name: name,
            Status: status,
            DueDate: dueDate,
            RepeatingInterval: repeatingInterval,
            UntilDate: untilDate);

    [Fact]
    public void TaskToResponseCarriesTheTasksColumns()
    {
        var response = TaskEndpoints.Respond(Recorded());

        Assert.Equal(
            new TaskResponse(
                21,
                14,
                "Follow-up call",
                "Ask about the household contacts",
                "high",
                Due,
                11,
                3,
                TaskEndpoints.OpenStatus,
                20,
                RecurringTaskRules.WeeklyCode,
                new DateOnly(2026, 4, 13),
                Raised,
                null),
            response);
    }

    /// <summary>
    /// A task with no due date, nobody named, no category and no schedule:
    /// every one of those columns is nullable, and a task that has none comes
    /// back with nulls rather than with zeros or a default date.
    /// </summary>
    [Fact]
    public void TaskToResponseCarriesABareTask()
    {
        var response = TaskEndpoints.Respond(new CaseTask { Id = 22, CreatedAt = Raised });

        Assert.Equal(
            new TaskResponse(
                22, null, null, null, null, null, null, null, null, null, null, null, Raised, null),
            response);
    }

    /// <summary>
    /// A completed task carries the moment it was changed, where a task
    /// nothing has touched carries none.
    /// </summary>
    [Fact]
    public void TaskToResponseCarriesTheMomentAStatusWasChanged()
    {
        var task = Recorded();
        task.Status = TaskEndpoints.CompleteStatus;
        task.UpdatedAt = Raised.AddDays(3);

        var response = TaskEndpoints.Respond(task);

        Assert.Equal(TaskEndpoints.CompleteStatus, response.Status);
        Assert.Equal(Raised.AddDays(3), response.UpdatedAt);
    }

    /// <summary>
    /// A task needs nothing but a name: the due date, the status, the
    /// schedule, the user and the category are each optional, and a task that
    /// is only a name is recorded.
    /// </summary>
    [Fact]
    public void ATaskThatIsOnlyANameIsNotRefused()
    {
        Assert.Null(TaskEndpoints.WhyNotATask(Asked()));
    }

    [Theory]
    [InlineData(TaskEndpoints.OpenStatus)]
    [InlineData(TaskEndpoints.CompleteStatus)]
    public void ATaskOfEitherStatusIsNotRefused(string status)
    {
        Assert.Null(TaskEndpoints.WhyNotATask(Asked(status: status)));
    }

    /// <summary>
    /// A task list of unnamed rows is of no use to whoever has to work it, so
    /// a task with no name is refused with the field named — whitespace
    /// included, because a box that was tabbed through is as empty as one that
    /// was not typed in.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ATaskWithNoNameIsRefused(string? name)
    {
        var why = TaskEndpoints.WhyNotATask(Asked(name: name));

        Assert.NotNull(why);
        Assert.Contains("name", why, StringComparison.Ordinal);
    }

    /// <summary>
    /// Each shape of status the endpoint refuses: empty, a state this tier
    /// does not work a task in, and one in the wrong case — the codes are
    /// stored codes and not display text. Null is not among them: a task may
    /// be recorded without a status, and only a status that is given is
    /// checked.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("in_progress")]
    [InlineData("Open")]
    public void ATaskOfAnUnknownStatusIsRefused(string status)
    {
        var why = TaskEndpoints.WhyNotATask(Asked(status: status));

        Assert.NotNull(why);
        Assert.Contains("status", why, StringComparison.Ordinal);
        Assert.Contains(TaskEndpoints.OpenStatus, why, StringComparison.Ordinal);
        Assert.Contains(TaskEndpoints.CompleteStatus, why, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TaskEndpoints.OpenStatus)]
    [InlineData(TaskEndpoints.CompleteStatus)]
    public void EitherStatusMayBeSetOnAStoredTask(string status)
    {
        Assert.Null(TaskEndpoints.WhyNotAStatus(status));
    }

    /// <summary>
    /// The status endpoint refuses an absent status as well as an unknown one:
    /// a task whose status is being set is being worked, and the screen that
    /// sets it offers the two.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("done")]
    public void NoOtherStatusMayBeSetOnAStoredTask(string? status)
    {
        var why = TaskEndpoints.WhyNotAStatus(status);

        Assert.NotNull(why);
        Assert.Contains("status", why, StringComparison.Ordinal);
    }

    /// <summary>
    /// A schedule that can be expanded is not refused, which is the case the
    /// recurring half of the slice rests on: a weekly task for a month.
    /// </summary>
    [Fact]
    public void ARepeatingTaskOnAScheduleThatCanBeExpandedIsNotRefused()
    {
        Assert.Null(TaskEndpoints.WhyNotATask(Asked(
            repeatingInterval: RecurringTaskRules.WeeklyCode,
            dueDate: Due,
            untilDate: Due.AddDays(28))));
    }

    /// <summary>
    /// What the schedule rule refuses, the endpoint refuses, in that rule's
    /// own words: an interval the vocabulary does not know, a repeat with no
    /// due date, an until date before the due date, and a series past the cap.
    /// The sentences are <c>RecurringTaskRules</c>'s and are proven there;
    /// what is proven here is that this slice asks it and passes it through
    /// rather than keeping a second copy of the schedule.
    /// </summary>
    [Theory]
    [InlineData("fortnightly", 0, 28)]
    [InlineData(RecurringTaskRules.WeeklyCode, null, 28)]
    [InlineData(RecurringTaskRules.DailyCode, 0, -1)]
    [InlineData(RecurringTaskRules.DailyCode, 0, 400)]
    public void ATaskOnAScheduleThatCannotBeExpandedIsRefusedInTheRulesWords(
        string repeatingInterval,
        int? dueOffset,
        int untilOffset)
    {
        var dueDate = dueOffset is { } offset ? Due.AddDays(offset) : (DateOnly?)null;
        var untilDate = Due.AddDays(untilOffset);

        var why = TaskEndpoints.WhyNotATask(Asked(
            repeatingInterval: repeatingInterval, dueDate: dueDate, untilDate: untilDate));

        Assert.Equal(
            RecurringTaskRules.WhyNotASchedule(dueDate, repeatingInterval, untilDate),
            why);
        Assert.NotNull(why);
    }

    /// <summary>
    /// The missing name is named before anything else, because a caller fixing
    /// one field at a time should be told about the one that makes the task a
    /// task.
    /// </summary>
    [Fact]
    public void TheMissingNameIsNamedBeforeTheSchedule()
    {
        var why = TaskEndpoints.WhyNotATask(Asked(
            name: null, repeatingInterval: "fortnightly"));

        Assert.NotNull(why);
        Assert.Contains("name", why, StringComparison.Ordinal);
        Assert.DoesNotContain("repeatingInterval", why, StringComparison.Ordinal);
    }
}
