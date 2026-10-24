namespace Survsys.Contracts;

/// <summary>
/// One case linked to an outbreak, as the outbreak's list of its cases shows
/// it: the case by its record number, the person it is about and the
/// condition.
/// </summary>
/// <param name="ReportId">The case morbidity report's identifier.</param>
/// <param name="RecordNumber">The record number the case is referred to by.</param>
/// <param name="PersonName">The person the case is about, as "Last, First".</param>
/// <param name="Condition">The condition the case is reported for.</param>
public record OutbreakCaseResponse(
    int ReportId,
    string RecordNumber,
    string PersonName,
    string Condition);
