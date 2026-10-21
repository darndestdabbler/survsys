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
public record ConditionDetailResponse(
    int Id,
    string Name,
    string? CdcCode,
    bool Active,
    string? ContactLeadIn,
    string? PlaceLeadIn,
    string? TreatmentLeadIn);
