using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader of the audits: <c>GET /api/audit</c>, the
/// changes to one table or one row of it, and <c>GET /api/access-audit</c>,
/// who signed in, opened, exported or searched. Both are paged and newest
/// first.
/// </summary>
/// <remarks>
/// The failure handling is <see cref="ApiAsk"/>'s, as every client's is; the
/// two asks carry their own prefixes.
/// </remarks>
public sealed class AuditClient(HttpClient http)
{
    /// <summary>How a failure to load the change audit starts.</summary>
    public const string ChangesFailurePrefix = "The change audit could not be loaded";

    /// <summary>How a failure to load the access audit starts.</summary>
    public const string AccessFailurePrefix = "The access audit could not be loaded";

    private static readonly ApiAsk ChangesAsk = new(ChangesFailurePrefix, "a page of the change audit");

    private static readonly ApiAsk AccessAsk = new(AccessFailurePrefix, "a page of the access audit");

    /// <summary>
    /// One page of the changes audited to a table, or to one row of it when a
    /// key is given, or why there is none.
    /// </summary>
    public Task<Asked<PagedResult<AuditEntryResponse>>> Changes(
        string table,
        string? key,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var path = $"api/audit?table={Uri.EscapeDataString(table)}&page={page}&pageSize={pageSize}";

        if (!string.IsNullOrWhiteSpace(key))
        {
            path += $"&key={Uri.EscapeDataString(key)}";
        }

        return ChangesAsk.Get<PagedResult<AuditEntryResponse>>(http, path, cancellationToken);
    }

    /// <summary>One page of the access audit, or why there is none.</summary>
    public Task<Asked<PagedResult<AccessRecordResponse>>> Access(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        AccessAsk.Get<PagedResult<AccessRecordResponse>>(
            http,
            $"api/access-audit?page={page}&pageSize={pageSize}",
            cancellationToken);
}
