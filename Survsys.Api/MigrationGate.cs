namespace Survsys.Api;

/// <summary>
/// The gate that keeps a database below the assembly's migration level from
/// being served from.
/// </summary>
/// <remarks>
/// <c>Survsys.Data</c>'s contract promises that such a database "fails at
/// startup, not at the first query that happens to touch a missing column".
/// This is that promise, and it lives in <c>Survsys.Api</c> because
/// <c>Survsys.Api</c> is the only thing that starts with a connection string.
///
/// It takes the pending-migration list rather than a <c>DbContext</c>, which
/// is the whole point of it being here at all: the decision is then a pure
/// function of that list, and a unit test can prove it without a database.
/// Everything that needs SQL Server — asking which migrations are pending —
/// stays at the call site.
/// </remarks>
public static class MigrationGate
{
    /// <summary>
    /// Lets startup continue when no migration is pending, and refuses
    /// loudly when any is, naming each one.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Any migration is pending. Thrown during startup, so the tier does not
    /// begin serving: one loud failure that names the gap, instead of a 500
    /// on whichever request first touches a missing column.
    /// </exception>
    public static void AssertNonePending(IEnumerable<string> pendingMigrations)
    {
        ArgumentNullException.ThrowIfNull(pendingMigrations);

        var pending = pendingMigrations.ToList();

        if (pending.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"The database is behind this assembly by {pending.Count} migration(s): "
            + $"{string.Join(", ", pending)}. Survsys.Api will not serve a database below "
            + "its own migration level. Apply the migrations — in a deployment, with the "
            + "idempotent script the handoff carries — and start the tier again.");
    }
}
