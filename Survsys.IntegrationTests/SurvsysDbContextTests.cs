using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The migration, against the database it is for. EF Core will build a model
/// that compiles and a migration that generates SQL no server ever accepted;
/// this is where that is found out.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class SurvsysDbContextTests(SqlServerFixture server)
{
    [Fact]
    public async Task TheMigrationAppliesAndLeavesNothingPending()
    {
        await using var context = server.NewContext();

        var applied = await context.Database.GetAppliedMigrationsAsync();
        var pending = await context.Database.GetPendingMigrationsAsync();

        Assert.NotEmpty(applied);
        Assert.Empty(pending);
    }

    /// <summary>
    /// Session 113 removed the system settings, which nothing read, and its
    /// migration drops their table.
    /// </summary>
    [Fact]
    public async Task TheSystemSettingTableIsDropped()
    {
        await using var context = server.NewContext();

        var tables = await context.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'system_setting'")
            .SingleAsync();

        Assert.Equal(0, tables);
    }

    /// <summary>
    /// The table the migration creates is the table the entity is mapped to,
    /// under the column names it was mapped with — a round trip through real
    /// SQL Server, not through the model EF built the migration from.
    /// </summary>
    [Fact]
    public async Task TheConditionsTableHoldsTheSeededRows()
    {
        await using var context = server.NewContext();

        var names = await context.Conditions
            .OrderBy(condition => condition.Name)
            .Select(condition => condition.Name)
            .ToListAsync();

        Assert.Equal(SqlServerFixture.ConditionsByName, names);
    }

    /// <summary>
    /// The nullable columns are nullable where the legacy data has nothing to
    /// put in them. Bartonellosis is a real condition with no CDC code.
    /// </summary>
    [Fact]
    public async Task AConditionWithNoCdcCodeRoundTripsAsNull()
    {
        await using var context = server.NewContext();

        var bartonellosis = await context.Conditions
            .SingleAsync(condition => condition.Name == "Bartonellosis");

        Assert.Null(bartonellosis.CdcCode);
        Assert.Null(bartonellosis.ContactLeadIn);
        Assert.Null(bartonellosis.ConditionGroupId);
        Assert.True(bartonellosis.Active);
    }

    /// <summary>
    /// The prose columns hold prose, at a length the schema does not cap. The
    /// legacy Hepatitis A lead-in runs to several sentences and carries both
    /// an escaped apostrophe and double quotes.
    /// </summary>
    [Fact]
    public async Task ALeadInColumnHoldsTheWholeLegacyProse()
    {
        await using var context = server.NewContext();

        var hepatitisA = await context.Conditions
            .SingleAsync(condition => condition.Name == "Hepatitis A");

        Assert.NotNull(hepatitisA.ContactLeadIn);
        Assert.Contains("isn't really a question", hepatitisA.ContactLeadIn, StringComparison.Ordinal);
        Assert.Contains("\"Was patient a contact", hepatitisA.ContactLeadIn, StringComparison.Ordinal);
    }
}
