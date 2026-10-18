using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Survsys.Data;

/// <summary>
/// Writes one <see cref="AuditEntry"/> for every row inserted, updated or
/// deleted in a save. It stands in for the legacy PostgreSQL row triggers that
/// wrote the audit inside the database itself: the move to a SQL Server schema
/// left no triggers, so the audit is kept here, where every context already
/// passes.
/// </summary>
/// <remarks>
/// The audit row is added to the saving context, so it goes in on the same
/// <c>SaveChanges</c> and the same transaction as the change it records: a save
/// rolled back takes its audit rows with it, and a save that commits commits
/// them too. The changed entries are snapshotted before any audit row is added,
/// so the audit never audits itself and never grows without end.
///
/// An inserted row's key is store-generated: the database assigns it only as
/// the row goes in, after <see cref="SavingChanges"/> has run, so the key the
/// audit row first carries is EF Core's temporary stand-in. Once the save has
/// assigned the real key, <see cref="SavedChanges"/> writes it over the
/// temporary one — in the same open transaction, so the correction is rolled
/// back with everything else. Updates and deletes already hold the real key
/// when they are audited and need no correction.
/// </remarks>
public sealed class AuditInterceptor : SaveChangesInterceptor
{
    private sealed record KeyFix(AuditEntry Audit, EntityEntry Source);

    // Carries the insert audit rows whose key was still temporary when added,
    // each with the entry it audits, from SavingChanges across to SavedChanges.
    // Keyed by context so a shared interceptor never crosses two contexts'
    // saves; a context saves one at a time, so the per-context list is safe.
    private readonly ConditionalWeakTable<DbContext, List<KeyFix>> _pendingKeyFixes = new();

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Audit(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Audit(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (TakePending(eventData.Context) is { } context)
        {
            context.SaveChanges();
        }

        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (TakePending(eventData.Context) is { } context)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void Audit(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        // Snapshot first: the AuditEntry rows added below would otherwise show
        // up as new Added entries and be audited in their turn, without end.
        var changed = context.ChangeTracker.Entries()
            .Where(entry => entry.Entity is not AuditEntry
                && entry.State is EntityState.Added
                    or EntityState.Modified
                    or EntityState.Deleted)
            .ToList();

        if (changed.Count == 0)
        {
            return;
        }

        var occurredAt = DateTime.UtcNow;
        List<KeyFix>? pending = null;

        foreach (var entry in changed)
        {
            var audit = new AuditEntry
            {
                TableName = entry.Metadata.GetTableName() ?? entry.Metadata.Name,
                KeyValues = RenderKey(entry),
                Operation = OperationFor(entry.State),
                OccurredAt = occurredAt,
            };
            context.Add(audit);

            if (entry.State is EntityState.Added)
            {
                (pending ??= []).Add(new KeyFix(audit, entry));
            }
        }

        if (pending is not null)
        {
            _pendingKeyFixes.AddOrUpdate(context, pending);
        }
    }

    /// <summary>
    /// Writes the real key over the temporary one on each insert's audit row
    /// and hands back the context to save again, or <c>null</c> when there is
    /// nothing to correct. Clearing the entry before the caller saves keeps the
    /// follow-up save — whose only change is these audit rows — from recurring.
    /// </summary>
    private DbContext? TakePending(DbContext? context)
    {
        if (context is null || !_pendingKeyFixes.TryGetValue(context, out var pending))
        {
            return null;
        }

        _pendingKeyFixes.Remove(context);

        foreach (var fix in pending)
        {
            fix.Audit.KeyValues = RenderKey(fix.Source);
        }

        return context;
    }

    private static string OperationFor(EntityState state) => state switch
    {
        EntityState.Added => "Insert",
        EntityState.Modified => "Update",
        EntityState.Deleted => "Delete",
        _ => state.ToString(),
    };

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
