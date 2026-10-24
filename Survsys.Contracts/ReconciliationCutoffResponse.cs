namespace Survsys.Contracts;

/// <summary>
/// The stored reconciliation cutoff: only cases reported in an MMWR week after
/// it are counted in the verification totals.
/// </summary>
/// <param name="MmwrYear">The MMWR year of the boundary.</param>
/// <param name="MmwrWeek">The MMWR week of the boundary, the last week not counted.</param>
public record ReconciliationCutoffResponse(int MmwrYear, int MmwrWeek);
