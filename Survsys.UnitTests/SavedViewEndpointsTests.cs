using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the saved-view endpoints decide without the database: the wire shape
/// and what a view to save must be.
/// </summary>
public class SavedViewEndpointsTests
{
    private static SaveSavedViewRequest View(
        string name = "Open salmonella",
        IReadOnlyList<string>? columns = null,
        string? workflowState = null,
        string? caseStatus = null,
        DateOnly? onsetFrom = null,
        DateOnly? onsetTo = null) =>
        new(name, columns ?? [ExportColumns.RecordNumber], 3, 2, workflowState, caseStatus, onsetFrom, onsetTo);

    [Fact]
    public void ToResponseCarriesTheViewWithItsColumnsInOrder()
    {
        var response = SavedViewEndpoints.ToResponse(new SavedView
        {
            Id = 4,
            UserId = 9,
            Name = "Open salmonella",
            Columns = "condition,record_number",
            ConditionId = 3,
            AgencyId = 2,
            WorkflowState = "accepted",
            CaseStatus = "C",
            OnsetFrom = new DateOnly(2026, 1, 1),
            OnsetTo = new DateOnly(2026, 12, 31),
        });

        Assert.Equal(
            new SavedViewResponse(4, "Open salmonella", response.Columns, 3, 2, "accepted", "C",
                new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)),
            response);
        Assert.Equal([ExportColumns.Condition, ExportColumns.RecordNumber], response.Columns);
    }

    [Fact]
    public void WhyNotASavedViewAcceptsANameColumnsAndFilters()
    {
        Assert.Null(SavedViewEndpoints.WhyNotASavedView(View(
            columns: [ExportColumns.RecordNumber, ExportColumns.LastName],
            workflowState: WorkflowRules.AcceptedCode,
            caseStatus: CaseStatusRules.ConfirmedCode,
            onsetFrom: new DateOnly(2026, 1, 1),
            onsetTo: new DateOnly(2026, 1, 1))));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void WhyNotASavedViewRefusesAViewWithNoName(string name)
    {
        Assert.Contains("name is required", SavedViewEndpoints.WhyNotASavedView(View(name)), StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotASavedViewRefusesANameLongerThanTheColumn()
    {
        Assert.Contains("name is longer than 100",
            SavedViewEndpoints.WhyNotASavedView(View(new string('a', 101))),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotASavedViewRefusesAViewWithNoColumns()
    {
        Assert.Contains("columns is required", SavedViewEndpoints.WhyNotASavedView(View(columns: [])), StringComparison.Ordinal);
        Assert.Contains("columns is required",
            SavedViewEndpoints.WhyNotASavedView(new SaveSavedViewRequest("Open salmonella", null)),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotASavedViewRefusesAColumnOutsideTheCatalogue()
    {
        Assert.Contains("columns holds 'ssn'",
            SavedViewEndpoints.WhyNotASavedView(View(columns: [ExportColumns.RecordNumber, "ssn"])),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotASavedViewRefusesARepeatedColumn()
    {
        Assert.Contains("columns holds 'last_name' more than once",
            SavedViewEndpoints.WhyNotASavedView(View(columns: [ExportColumns.LastName, ExportColumns.RecordNumber, ExportColumns.LastName])),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("reopened")]
    public void WhyNotASavedViewRefusesAnUnknownWorkflowState(string state)
    {
        Assert.Contains("workflowState", SavedViewEndpoints.WhyNotASavedView(View(workflowState: state)), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("X")]
    public void WhyNotASavedViewRefusesAnUnknownCaseStatus(string status)
    {
        Assert.Contains("caseStatus", SavedViewEndpoints.WhyNotASavedView(View(caseStatus: status)), StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotASavedViewRefusesAnOnsetRangeThatRunsBackwards()
    {
        Assert.Contains("onsetFrom is after onsetTo",
            SavedViewEndpoints.WhyNotASavedView(View(onsetFrom: new DateOnly(2026, 2, 1), onsetTo: new DateOnly(2026, 1, 1))),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAGroupingAcceptsOneOfTheViewsColumns()
    {
        Assert.Null(SavedViewEndpoints.WhyNotAGrouping(
            [ExportColumns.RecordNumber, ExportColumns.Condition], ExportColumns.Condition));
    }

    /// <summary>
    /// A column the catalogue knows but the view does not export is refused:
    /// the report counts by what the view shows.
    /// </summary>
    [Fact]
    public void WhyNotAGroupingRefusesAColumnTheViewDoesNotExport()
    {
        Assert.Contains("record_number, condition",
            SavedViewEndpoints.WhyNotAGrouping([ExportColumns.RecordNumber, ExportColumns.Condition], ExportColumns.Agency),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAGroupingRefusesNoGrouping()
    {
        Assert.NotNull(SavedViewEndpoints.WhyNotAGrouping([ExportColumns.RecordNumber], null));
    }
}
