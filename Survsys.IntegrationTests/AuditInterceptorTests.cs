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

    private async Task<string> Insert()
    {
        await using var context = server.NewContext();

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
