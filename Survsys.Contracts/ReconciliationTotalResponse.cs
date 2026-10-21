namespace Survsys.Contracts;

/// <summary>
/// One verification total: how many cases of a condition in an MMWR year CDC
/// accepted that the reconciliation cutoff counts.
/// </summary>
/// <param name="Condition">The condition, by name.</param>
/// <param name="CdcCode">The condition's CDC code, or null where it has none.</param>
/// <param name="MmwrYear">The MMWR year the cases were reported in.</param>
/// <param name="Count">How many cases are counted.</param>
public record ReconciliationTotalResponse(string Condition, string? CdcCode, int MmwrYear, int Count);
