namespace Survsys.Contracts;

/// <summary>
/// A condition to create or to save over an existing one: what
/// <c>POST /api/conditions</c> and <c>PUT /api/conditions/{id}</c> take.
/// </summary>
/// <remarks>
/// A blank optional value is stored as null, as the conditions fixture stores
/// the legacy's empty strings.
/// </remarks>
/// <param name="Name">The condition's name. Required.</param>
/// <param name="Active">Whether the condition may be chosen for a new report.</param>
/// <param name="CdcCode">The CDC's code for the condition, or null for none.</param>
/// <param name="ContactLeadIn">The investigator instructions shown above the Contacts tab, or null.</param>
/// <param name="PlaceLeadIn">The same, for exposure places.</param>
/// <param name="TreatmentLeadIn">The same, for treatments.</param>
public record SaveConditionRequest(
    string Name,
    bool Active,
    string? CdcCode = null,
    string? ContactLeadIn = null,
    string? PlaceLeadIn = null,
    string? TreatmentLeadIn = null);
