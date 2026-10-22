using System.Globalization;
using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader of the ad-hoc reports:
/// <c>GET /api/reports/case-counts</c>, case counts grouped by one dimension
/// over an optional event onset range, and
/// <c>GET /api/reports/saved-views/{id}/counts</c>, the reports one of the
/// user's saved views selects counted by one of its columns, with its CSV.
/// </summary>
/// <remarks>
/// It is the only thing in this tier that speaks HTTP about reports, and the
/// failure handling is <see cref="ApiAsk"/>'s, as every client's is. A date is
/// in the query string only when one is given, so an open-ended range is
/// asked for as one.
/// </remarks>
public sealed class ReportsClient(HttpClient http)
{
    /// <summary>
    /// How a failure to run a report starts. Public because the screen and the
    /// unit suite both read it rather than repeating the wording.
    /// </summary>
    public const string FailurePrefix = "The report could not be run";

    private static readonly ApiAsk Running = new(FailurePrefix, "the case counts");

    private static readonly ApiAsk Exporting = new(FailurePrefix, "the case counts as CSV");

    /// <summary>
    /// The case counts grouped by <paramref name="groupBy"/>, over the event
    /// onset range given, or why there are none.
    /// </summary>
    public Task<Asked<IReadOnlyList<CaseCountResponse>>> CaseCounts(
        string groupBy,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var query = $"api/reports/case-counts?groupBy={Uri.EscapeDataString(groupBy)}";

        if (from is { } start)
        {
            query += $"&from={start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        }

        if (to is { } end)
        {
            query += $"&to={end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        }

        return Running.Get<IReadOnlyList<CaseCountResponse>>(http, query, cancellationToken);
    }

    /// <summary>
    /// The reports saved view <paramref name="id"/> selects, counted by its
    /// column <paramref name="groupBy"/>, or why there are none.
    /// </summary>
    public Task<Asked<IReadOnlyList<CaseCountResponse>>> SavedViewCounts(
        int id,
        string groupBy,
        CancellationToken cancellationToken = default) =>
        Running.Get<IReadOnlyList<CaseCountResponse>>(
            http, $"api/reports/saved-views/{id}/counts?groupBy={Uri.EscapeDataString(groupBy)}", cancellationToken);

    /// <summary>The same counts as CSV of the column and its count, or why there is none.</summary>
    public Task<Asked<string>> SavedViewCountsCsv(
        int id,
        string groupBy,
        CancellationToken cancellationToken = default) =>
        Exporting.GetText(
            http, $"api/reports/saved-views/{id}/counts/export?groupBy={Uri.EscapeDataString(groupBy)}", cancellationToken);
}
