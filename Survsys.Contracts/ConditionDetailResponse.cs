namespace Survsys.Contracts;

/// <summary>
/// One condition as its administration screen shows it: every stored column
/// but the group, which points at a table that does not exist yet.
/// </summary>
/// <param name="Id">The condition's identifier.</param>
/// <param name="Name">The condition's name.</param>
/// <param name="CdcCode">The CDC's code for the condition, or null where it has none.</param>
/// <param name="Active">Whether the condition may be chosen for a new report.</param>
/// <param name="ContactLeadIn">The investigator instructions shown above the Contacts tab, or null.</param>
/// <param name="PlaceLeadIn">The same, for exposure places.</param>
/// <param name="TreatmentLeadIn">The same, for treatments.</param>
/// <param name="GuideId">
/// The notification guide the condition's cases are exported under, or null
/// where it has none. Answered by the condition's own read and save; a list of
/// conditions, and the conditions CSV, leave it null.
/// </param>
/// <param name="BypassOutOfState">Whether a lab message for the condition is assigned though its patient is out of state.</param>
/// <param name="BlockPreliminary">Whether a lab message for the condition whose every result is preliminary is filtered.</param>
public record ConditionDetailResponse(
    int Id,
    string Name,
    string? CdcCode,
    bool Active,
    string? ContactLeadIn,
    string? PlaceLeadIn,
    string? TreatmentLeadIn,
    int? GuideId = null,
    bool BypassOutOfState = false,
    bool BlockPreliminary = false);
