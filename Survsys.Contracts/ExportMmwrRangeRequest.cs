namespace Survsys.Contracts;

/// <summary>
/// An MMWR range to export to CDC: what <c>POST /api/notification-queue/mmwr-range</c>
/// takes, queueing the morbidity cases of the range whose print criteria pass.
/// </summary>
/// <param name="MmwrYear">The MMWR year.</param>
/// <param name="StartWeek">The first MMWR week of the range, from 1.</param>
/// <param name="EndWeek">The last MMWR week of the range, inclusive, up to 53.</param>
/// <param name="ConditionId">One condition to export, or null for every condition.</param>
public record ExportMmwrRangeRequest(int MmwrYear, int StartWeek, int EndWeek, int? ConditionId = null);
