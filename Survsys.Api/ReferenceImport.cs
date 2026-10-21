using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// What distinguishes one reference table from another for its CSV import and
/// export: its rows, its CSV form, its natural key, and how a row is put on an
/// entry. The import and export common to every table are
/// <see cref="ReferenceImport"/>'s.
/// </summary>
internal abstract class ReferenceTable<TEntity, TRow>
    where TEntity : class
    where TRow : class
{
    public abstract IReadOnlyList<string> Columns { get; }

    /// <summary>
    /// Every row of the table, in a stable order. Ordered once read, because
    /// a projected row's members do not translate to SQL and the tables are
    /// small.
    /// </summary>
    public abstract Task<List<TRow>> List(SurvsysDbContext database, CancellationToken cancellationToken);

    public abstract IReadOnlyList<string?> Fields(TRow row);

    public abstract (TRow? Row, string? Why) Parse(CsvRow row);

    public abstract string? WhyNot(TRow row);

    /// <summary>The row's natural key as an import reports it.</summary>
    public abstract string KeyOf(TRow row);

    /// <summary>The entry already on file under the row's key, or null.</summary>
    public abstract Task<TEntity?> ByKey(SurvsysDbContext database, TRow row, CancellationToken cancellationToken);

    /// <summary>An entry on file read back as a row, as the list reads it.</summary>
    public abstract Task<TRow> RowOf(SurvsysDbContext database, TEntity entity, CancellationToken cancellationToken);

    /// <summary>
    /// Puts the row's values on the entity, resolving what it names, or says
    /// which of those is not on file.
    /// </summary>
    public abstract Task<string?> Apply(
        SurvsysDbContext database, TEntity entity, TRow row, CancellationToken cancellationToken);

    public abstract TEntity New(TRow row);

    /// <summary>
    /// What the row puts on file beside its entry once the entry is saved and
    /// has its id — a test type's conditions, say. Nothing, unless a table
    /// says otherwise.
    /// </summary>
    public virtual Task Saved(
        SurvsysDbContext database, TEntity entity, TRow row, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// The CSV import and export every reference table shares: an export of every
/// row under a header row, and an import previewed before it is confirmed.
/// </summary>
/// <remarks>
/// A row is matched to an entry by the table's natural key: one with no entry
/// is added, one that differs from its entry is saved over it, one that
/// matches it field for field is left alone, and one that cannot be saved is
/// refused with its line and why rather than failing the file. An entry the
/// file leaves out is kept. A preview runs exactly the saves a confirmed
/// import does, inside a transaction it rolls back, so what it reports is what
/// the confirmation will do and nothing is written until then.
/// </remarks>
internal static class ReferenceImport
{
    public static async Task<string> Export<TEntity, TRow>(
        SurvsysDbContext database, ReferenceTable<TEntity, TRow> table, CancellationToken cancellationToken)
        where TEntity : class
        where TRow : class =>
        ReferenceCsv.Write(table.Columns, (await table.List(database, cancellationToken)).Select(table.Fields));

    /// <summary>
    /// What importing the CSV file would do, written only where
    /// <paramref name="confirm"/> is true.
    /// </summary>
    public static async Task<ReferenceImportResponse> Import<TEntity, TRow>(
        SurvsysDbContext database,
        ReferenceTable<TEntity, TRow> table,
        string? csv,
        bool confirm,
        CancellationToken cancellationToken)
        where TEntity : class
        where TRow : class
    {
        var (rows, refused) = ReferenceCsv.Read(csv, table.Columns);
        var problems = refused.ToList();
        var added = new List<ReferenceImportRow>();
        var changed = new List<ReferenceImportRow>();

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        foreach (var csvRow in rows)
        {
            var (row, why) = table.Parse(csvRow);

            if ((row is null ? why : table.WhyNot(row)) is { } refusal)
            {
                problems.Add(new ContactImportProblem(csvRow.Line, refusal));
                continue;
            }

            var holder = await table.ByKey(database, row!, cancellationToken);

            if (holder is not null
                && table.Fields(await table.RowOf(database, holder, cancellationToken)).SequenceEqual(table.Fields(row!)))
            {
                continue;
            }

            var entity = holder ?? table.New(row!);

            if (await table.Apply(database, entity, row!, cancellationToken) is { } unresolved)
            {
                problems.Add(new ContactImportProblem(csvRow.Line, unresolved));
                database.ChangeTracker.Clear();
                continue;
            }

            if (holder is null)
            {
                database.Add(entity);
            }

            await database.SaveChangesAsync(cancellationToken);
            await table.Saved(database, entity, row!, cancellationToken);
            (holder is null ? added : changed).Add(new ReferenceImportRow(csvRow.Line, table.KeyOf(row!)));
        }

        if (confirm)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(cancellationToken);
        }

        database.ChangeTracker.Clear();

        return new ReferenceImportResponse(
            confirm, added, changed, problems.OrderBy(problem => problem.Line).ToList());
    }
}
