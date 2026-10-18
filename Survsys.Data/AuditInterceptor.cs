using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace Survsys.Data;

/// <summary>
/// Writes one <see cref="AuditEntry"/> for every row inserted, updated or
/// deleted in a save. It stands in for the legacy PostgreSQL row triggers that
/// wrote the audit inside the database itself: the move to a SQL Server schema
/// left no triggers, so the audit is kept here, where every context passes.
/// </summary>
/// <remarks>
/// The changed rows are captured as the save begins and the audit rows are
/// written once it has succeeded (<see cref="SavedChanges"/>), by then the
/// database has assigned every store-generated key, so an inserted row is
/// audited with its real key and not EF Core's temporary stand-in. The audit
/// rows are written straight to the table rather than through the change
/// tracker, so the save is never asked to replay the rows it just wrote and a
/// retried save cannot double them; and they share the change's transaction —
/// the caller's when one is open, otherwise one this interceptor opens for the
/// save and commits only once the audit is in — so a rollback takes the audit
/// with it. A failed save writes no audit at all, because the audit is written
/// only on success. An <see cref="AuditEntry"/> is never itself audited.
/// </remarks>
public sealed class AuditInterceptor : SaveChangesInterceptor
{
    private sealed record Change(string TableName, string Operation, EntityEntry Source, string? DeletedKey);

    private sealed class Pending
    {
        public required IReadOnlyList<Change> Changes { get; init; }

        public required DateTime OccurredAt { get; init; }

        /// <summary>The transaction this interceptor opened, or null when the
        /// caller's own transaction is carrying the save.</summary>
        public IDbContextTransaction? OwnedTransaction { get; set; }
    }

    // Carries one save's captured changes from the start of the save across to
    // its completion. Keyed by context so a shared interceptor never crosses
    // two contexts' saves; a context saves one at a time, so this is safe.
    private readonly ConditionalWeakTable<DbContext, Pending> _pending = new();

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        var context = eventData.Context;
        if (Capture(context) is { } pending && context!.Database.CurrentTransaction is null)
        {
            pending.OwnedTransaction = context.Database.BeginTransaction();
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (Capture(context) is { } pending && context!.Database.CurrentTransaction is null)
        {
            pending.OwnedTransaction = await context.Database.BeginTransactionAsync(cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (TakePending(eventData.Context) is { } pending)
        {
            var context = eventData.Context!;
            foreach (var change in pending.Changes)
            {
                context.Database.ExecuteSqlInterpolated(InsertSql(change, pending.OccurredAt));
            }

            if (pending.OwnedTransaction is { } transaction)
            {
                transaction.Commit();
                transaction.Dispose();
            }
        }

        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (TakePending(eventData.Context) is { } pending)
        {
            var context = eventData.Context!;
            foreach (var change in pending.Changes)
            {
                await context.Database.ExecuteSqlInterpolatedAsync(
                    InsertSql(change, pending.OccurredAt), cancellationToken);
            }

            if (pending.OwnedTransaction is { } transaction)
            {
                await transaction.CommitAsync(cancellationToken);
                await transaction.DisposeAsync();
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (TakePending(eventData.Context) is { OwnedTransaction: { } transaction })
        {
            transaction.Rollback();
            transaction.Dispose();
        }

        base.SaveChangesFailed(eventData);
    }

    public override async Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (TakePending(eventData.Context) is { OwnedTransaction: { } transaction })
        {
            await transaction.RollbackAsync(cancellationToken);
            await transaction.DisposeAsync();
        }

        await base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private Pending? Capture(DbContext? context)
    {
        if (context is null)
        {
            return null;
        }

        var changes = new List<Change>();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is AuditEntry)
            {
                continue;
            }

            var operation = entry.State switch
            {
                EntityState.Added => "Insert",
                EntityState.Modified => "Update",
                EntityState.Deleted => "Delete",
                _ => null,
            };
            if (operation is null)
            {
                continue;
            }

            // A deleted row detaches once the save completes, so its key is
            // rendered now, while the entry still carries one; an inserted row's
            // key is assigned by the save, so it is left to be rendered after.
            var deletedKey = entry.State is EntityState.Deleted ? RenderKey(entry) : null;
            changes.Add(new Change(
                entry.Metadata.GetTableName() ?? entry.Metadata.Name,
                operation,
                entry,
                deletedKey));
        }

        if (changes.Count == 0)
        {
            return null;
        }

        var pending = new Pending { Changes = changes, OccurredAt = DateTime.UtcNow };
        _pending.AddOrUpdate(context, pending);
        return pending;
    }

    private Pending? TakePending(DbContext? context)
    {
        if (context is null || !_pending.TryGetValue(context, out var pending))
        {
            return null;
        }

        _pending.Remove(context);
        return pending;
    }

    // The identifiers mirror AuditEntryConfiguration; the integration suite
    // inserts through this path against the real schema, so a drift from the
    // mapping fails there rather than silently. The values are interpolated,
    // so EF Core sends them as parameters, never as text spliced into the SQL.
    private static FormattableString InsertSql(Change change, DateTime occurredAt)
    {
        var key = change.DeletedKey ?? RenderKey(change.Source);
        return $"INSERT INTO audit_entries (table_name, key_values, operation, occurred_at) VALUES ({change.TableName}, {key}, {change.Operation}, {occurredAt})";
    }

    private static string RenderKey(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null)
        {
            return string.Empty;
        }

        return string.Join(
            ",",
            key.Properties.Select(property =>
                entry.Property(property.Name).CurrentValue?.ToString() ?? string.Empty));
    }
}
