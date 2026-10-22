namespace Survsys.Contracts;

/// <summary>
/// The eCR quality figures for one sender and one condition over the
/// documents received in the window asked for: what <c>GET /api/ecr-quality</c>
/// answers a list of.
/// </summary>
/// <param name="Sender">
/// The eICRs' sender (their custodian id), an RR counted under its eICR's;
/// null for RRs whose eICR is not on file, and for documents that name none.
/// </param>
/// <param name="SenderName">The registered sender's name, or null where it is not registered.</param>
/// <param name="Condition">
/// The condition the RRs keep: the mapped catalogue condition's name, else the
/// code's display name, else the code; null for documents with none.
/// </param>
/// <param name="Eicrs">The eICRs received; one with several conditions is counted under each.</param>
/// <param name="Rrs">The RRs received.</param>
/// <param name="Errors">The validation errors (fatal or error) of those documents.</param>
/// <param name="Warnings">The validation warnings of those documents.</param>
/// <param name="AverageLatencyMinutes">The eICRs' average receipt latency, received less created, in minutes; null where none says when it was created.</param>
/// <param name="LongestLatencyMinutes">The longest of those latencies, in minutes, or null.</param>
/// <param name="EicrsWithoutRr">The eICRs no RR on file responds to.</param>
/// <param name="RrsWithoutEicr">The RRs whose eICR is not on file.</param>
public record EcrQualityRowResponse(
    string? Sender,
    string? SenderName,
    string? Condition,
    int Eicrs,
    int Rrs,
    int Errors,
    int Warnings,
    double? AverageLatencyMinutes,
    double? LongestLatencyMinutes,
    int EicrsWithoutRr,
    int RrsWithoutEicr);
