namespace Survsys.Data;

/// <summary>
/// That a row was inserted, updated or deleted, and when: the change audit the
/// <see cref="AuditInterceptor"/> writes on every save, standing in for the
/// legacy PostgreSQL row triggers that wrote one <c>audit</c> row per changed
/// row.
/// </summary>
/// <remarks>
/// Written and never changed. A row records the table it audits, the primary
/// key of the affected row rendered to text, the operation, the time, who made
/// the change and the row's values before and after as JSON. Who-viewed-what is
/// the access audit's (<see cref="AccessRecord"/>).
/// </remarks>
public class AuditEntry
{
    public int Id { get; set; }

    /// <summary>The audited table, as the model maps it.</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>The affected row's primary key, rendered to text.</summary>
    public string KeyValues { get; set; } = string.Empty;

    /// <summary>The operation: <c>Insert</c>, <c>Update</c> or <c>Delete</c>.</summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>When the change was saved, in UTC.</summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>
    /// The user who made the change, or null where none was signed in. A plain
    /// id with no foreign key, so the audit outlives the user.
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>The row's values before the change, as JSON; null for an insert.</summary>
    public string? OldValues { get; set; }

    /// <summary>The row's values after the change, as JSON; null for a delete.</summary>
    public string? NewValues { get; set; }
}
