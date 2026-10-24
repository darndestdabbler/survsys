namespace Survsys.Contracts;

/// <summary>
/// The reconciliation cutoff to store: what <c>PUT /api/reconciliation-cutoff</c> takes.
/// </summary>
/// <param name="MmwrYear">The MMWR year of the boundary.</param>
/// <param name="MmwrWeek">The MMWR week of the boundary, 1 to 53: the last week not counted.</param>
public record ReconciliationCutoffRequest(int MmwrYear, int MmwrWeek);
