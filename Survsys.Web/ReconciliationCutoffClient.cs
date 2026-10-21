using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of the reconciliation cutoff:
/// <c>GET</c> and <c>PUT /api/reconciliation-cutoff</c>, and the verification
/// totals it bounds, <c>GET /api/reconciliation-cutoff/totals</c>.
/// </summary>
/// <remarks>
/// The failure handling is <see cref="ApiAsk"/>'s, as every client's is, with
/// a prefix for each ask. No cutoff set is the API tier's 404, which comes
/// back here as a failure like any other; the screen reads a failed find as
/// "none set" only alongside totals that did load.
/// </remarks>
public sealed class ReconciliationCutoffClient(HttpClient http)
{
    /// <summary>How a failure to load the cutoff starts.</summary>
    public const string FindFailurePrefix = "The reconciliation cutoff could not be loaded";

    /// <summary>How a failure to store the cutoff starts.</summary>
    public const string SaveFailurePrefix = "The reconciliation cutoff could not be saved";

    /// <summary>How a failure to load the verification totals starts.</summary>
    public const string TotalsFailurePrefix = "The reconciliation totals could not be loaded";

    private static readonly ApiAsk Finding = new(FindFailurePrefix, "the reconciliation cutoff");

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the reconciliation cutoff as stored");

    private static readonly ApiAsk Totaling = new(TotalsFailurePrefix, "the reconciliation totals");

    /// <summary>The stored cutoff, or why there is none — a 404 where none is set.</summary>
    public Task<Asked<ReconciliationCutoffResponse>> Find(CancellationToken cancellationToken = default) =>
        Finding.Get<ReconciliationCutoffResponse>(http, "api/reconciliation-cutoff", cancellationToken);

    /// <summary>Stores the cutoff, answering with it as stored or with why it was not.</summary>
    public Task<Asked<ReconciliationCutoffResponse>> Set(
        int mmwrYear,
        int mmwrWeek,
        CancellationToken cancellationToken = default) =>
        Saving.Put<ReconciliationCutoffRequest, ReconciliationCutoffResponse>(
            http, "api/reconciliation-cutoff", new ReconciliationCutoffRequest(mmwrYear, mmwrWeek), cancellationToken);

    /// <summary>The verification totals the cutoff bounds, or why there are none.</summary>
    public Task<Asked<List<ReconciliationTotalResponse>>> Totals(CancellationToken cancellationToken = default) =>
        Totaling.Get<List<ReconciliationTotalResponse>>(http, "api/reconciliation-cutoff/totals", cancellationToken);
}
