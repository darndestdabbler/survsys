using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The <see cref="AuditInterceptor"/>, against the real database: that saving
/// an insert, an update and a delete each writes the audit row the legacy
/// triggers used to, carrying the affected row's table and key, and that a save
/// rolled back leaves none behind.
/// </summary>
/// <remarks>
/// The subject is a <see cref="Treatment"/> because it has no foreign keys, so
/// a row can be inserted, updated and deleted on its own. Every assertion is
/// scoped to the <c>treatments</c> table and the subject's key — the suite
/// shares the one database, so a count over the whole table would be a claim
/// about every other test too. The seed data is loaded by raw SQL, which the
/// interceptor never sees, so it contributes no audit rows; the tests do not
/// lean on that beyond scoping to their own key.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class AuditInterceptorTests(SqlServerFixture server)
{
    private const string Table = "treatments";

    private const int AuditUser = 4242;

    [Fact]
    public async Task AnInsertWritesOneInsertRowForTheTreatmentKey()
    {
        var key = await Insert();

        var operations = await AuditOperations(key);

        Assert.Equal(["Insert"], operations);
    }

    [Fact]
    public async Task AnUpdateWritesAnUpdateRowForTheSameKey()
    {
        var key = await Insert();

        await using (var context = server.NewContext())
        {
            var treatment = await context.Treatments.SingleAsync(t => t.Id == int.Parse(key));
            treatment.Active = !treatment.Active;
            await context.SaveChangesAsync();
        }

        var operations = await AuditOperations(key);

        Assert.Equal(["Insert", "Update"], operations);
    }

    [Fact]
    public async Task ADeleteWritesADeleteRowForTheSameKey()
    {
        var key = await Insert();

        await using (var context = server.NewContext())
        {
            var treatment = await context.Treatments.SingleAsync(t => t.Id == int.Parse(key));
            context.Treatments.Remove(treatment);
            await context.SaveChangesAsync();
        }

        var operations = await AuditOperations(key);

        Assert.Equal(["Insert", "Delete"], operations);
    }

    [Fact]
    public async Task AnInsertRolledBackWritesNoAuditRow()
    {
        string key;

        await using (var context = server.NewContext())
        {
            await using var transaction = await context.Database.BeginTransactionAsync();

            var treatment = NewTreatment();
            context.Treatments.Add(treatment);
            await context.SaveChangesAsync();
            key = treatment.Id.ToString();

            // Disposed without a commit, so the insert and the audit row it
            // carried both roll back.
            await transaction.RollbackAsync();
        }

        var operations = await AuditOperations(key);

        Assert.Empty(operations);
    }

    [Fact]
    public async Task ARetryAfterAFailedSaveWritesOneInsertRowWithTheRealKey()
    {
        await using var context = server.NewContext();

        var treatment = NewTreatment();
        treatment.Name = new string('x', 500); // Over the column's 200, so the insert is refused.
        context.Treatments.Add(treatment);
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync());

        // The same entry, fixed and saved again on the same context.
        treatment.Name = "Audit retry " + Guid.NewGuid();
        await context.SaveChangesAsync();

        var operations = await AuditOperations(treatment.Id.ToString());

        Assert.Equal(["Insert"], operations);
    }

    [Fact]
    public async Task AnInsertWithoutAcceptingChangesIsNotReplayed()
    {
        var name = "Audit no-accept " + Guid.NewGuid();

        await using (var context = server.NewContext())
        {
            var treatment = NewTreatment();
            treatment.Name = name;
            context.Treatments.Add(treatment);

            // Not accepting changes leaves the entity's generated key unset, so
            // the real key is read back below rather than from the entity.
            await context.SaveChangesAsync(acceptAllChangesOnSuccess: false);
        }

        await using var verify = server.NewContext();

        // The source insert ran once, not twice.
        var ids = await verify.Treatments.Where(t => t.Name == name).Select(t => t.Id).ToListAsync();
        var key = Assert.Single(ids).ToString();

        Assert.Equal(["Insert"], await AuditOperations(key));
    }

    [Fact]
    public async Task AnInsertRecordsTheUserAndOnlyTheNewValues()
    {
        var key = await Insert(AuditUser);

        var entry = Assert.Single(await Entries(Table, key));

        Assert.Equal(AuditUser, entry.UserId);
        Assert.Null(entry.OldValues);
        Assert.Equal(true, Value(entry.NewValues, "active").GetBoolean());
        Assert.StartsWith("Audit subject ", Value(entry.NewValues, "treatment_name").GetString());
    }

    [Fact]
    public async Task AnUpdateRecordsTheUserAndTheValuesBeforeAndAfter()
    {
        var key = await Insert();

        await using (var context = server.NewContext(AuditUser))
        {
            var treatment = await context.Treatments.SingleAsync(t => t.Id == int.Parse(key));
            treatment.Active = false;
            await context.SaveChangesAsync();
        }

        var update = (await Entries(Table, key)).Single(entry => entry.Operation == "Update");

        Assert.Equal(AuditUser, update.UserId);
        Assert.True(Value(update.OldValues, "active").GetBoolean());
        Assert.False(Value(update.NewValues, "active").GetBoolean());
    }

    [Fact]
    public async Task ADeleteRecordsTheUserAndOnlyTheOldValues()
    {
        var key = await Insert();

        await using (var context = server.NewContext(AuditUser))
        {
            context.Treatments.Remove(await context.Treatments.SingleAsync(t => t.Id == int.Parse(key)));
            await context.SaveChangesAsync();
        }

        var delete = (await Entries(Table, key)).Single(entry => entry.Operation == "Delete");

        Assert.Equal(AuditUser, delete.UserId);
        Assert.Null(delete.NewValues);
        Assert.StartsWith("Audit subject ", Value(delete.OldValues, "treatment_name").GetString());
    }

    [Fact]
    public async Task AChangeSavedWithNoUserRecordsNone()
    {
        var key = await Insert();

        Assert.Null(Assert.Single(await Entries(Table, key)).UserId);
    }

    [Fact]
    public async Task AVocabularyRowInsertedIsAudited()
    {
        string key;

        await using (var context = server.NewContext(AuditUser))
        {
            var organism = new VocabMasterOrganism
            {
                Snomed = "AUDIT-" + Guid.NewGuid().ToString("N")[..8],
                OrganismName = "Audit organism",
            };
            context.VocabMasterOrganisms.Add(organism);
            await context.SaveChangesAsync();
            key = organism.Id.ToString();
        }

        var entry = Assert.Single(await Entries("vocab_master_organism", key));

        Assert.Equal("Insert", entry.Operation);
        Assert.Equal(AuditUser, entry.UserId);
        Assert.Equal("Audit organism", Value(entry.NewValues, "organism").GetString());
    }

    private async Task<string> Insert(int? userId = null)
    {
        await using var context = server.NewContext(userId);

        var treatment = NewTreatment();
        context.Treatments.Add(treatment);
        await context.SaveChangesAsync();

        return treatment.Id.ToString();
    }

    private static Treatment NewTreatment() => new()
    {
        Name = "Audit subject " + Guid.NewGuid(),
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private async Task<List<AuditEntry>> Entries(string table, string key)
    {
        await using var context = server.NewContext();

        return await context.Set<AuditEntry>()
            .Where(entry => entry.TableName == table && entry.KeyValues == key)
            .OrderBy(entry => entry.Id)
            .ToListAsync();
    }

    private static JsonElement Value(string? json, string column) =>
        JsonDocument.Parse(json!).RootElement.GetProperty(column);

    /// <summary>
    /// The operations audited against the <c>treatments</c> table for one key,
    /// in the order they were written, read back through a fresh context.
    /// </summary>
    private async Task<List<string>> AuditOperations(string key)
    {
        await using var context = server.NewContext();

        return await context.Set<AuditEntry>()
            .Where(entry => entry.TableName == Table && entry.KeyValues == key)
            .OrderBy(entry => entry.Id)
            .Select(entry => entry.Operation)
            .ToListAsync();
    }
}
