namespace Survsys.Contracts;

/// <summary>
/// A case morbidity report to open: what
/// <c>POST /api/person-conditions</c> takes.
/// </summary>
/// <remarks>
/// The four things step two of the legacy creation flow knows — the person the
/// report is about, the disease it is for, the agency investigating it and the
/// date the disease was first reported to public health. The first two are the
/// report; the screen asks for the last two, as
/// <c>../nedss/docs/functional/epitrax-cmr-workflow.md</c> records its three
/// fields.
///
/// <strong>The record number is not here, and cannot be.</strong> It is
/// assigned by the tier that stores the report, from the year and the highest
/// number already issued in it: a caller that could choose one could choose
/// one already taken.
///
/// Neither is anything else the legacy table holds. The workflow status, the
/// case status, the MMWR week, the investigator, the queue and every clinical,
/// laboratory, investigation and administrative field belong to the sessions
/// that show them, and a report is created without them.
/// </remarks>
/// <param name="PersonId">The person the report is about.</param>
/// <param name="ConditionId">
/// The disease or condition the report is for, which must be one that may be
/// chosen for a new report.
/// </param>
/// <param name="AgencyId">
/// The agency investigating the report, as <c>GET /api/agencies</c> lists
/// them, or null where none has been chosen. Nullable because the legacy
/// column is: the creation screen asks for one, and a report can exist
/// without one.
/// </param>
/// <param name="FirstReportedPhDate">
/// The date the condition was first reported to public health, where it is
/// known.
/// </param>
public record CreatePersonConditionRequest(
    int PersonId,
    int ConditionId,
    int? AgencyId = null,
    DateOnly? FirstReportedPhDate = null);
