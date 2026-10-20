namespace Survsys.Contracts;

/// <summary>
/// A disease or condition, as a list screen shows it.
/// </summary>
/// <remarks>
/// Four of the stored table's eight columns. The three lead-in prose columns
/// — paragraphs of investigator instruction for the Contacts, exposure-place
/// and treatment sections — are not what a list shows, and the group
/// identifier points at a table that does not exist yet; all four stay off
/// the wire until a screen asks for them. The mapping that drops them lives
/// in <c>Survsys.Api</c>, the tier that owns the wire, because
/// <c>Survsys.Data</c> may not reference this project.
/// </remarks>
/// <param name="Id">The condition's identifier, as every other resource refers to it.</param>
/// <param name="Name">The condition's name.</param>
/// <param name="CdcCode">
/// The CDC's code for the condition, or null where it is not nationally
/// notifiable and has none.
/// </param>
/// <param name="Active">Whether the condition may be chosen for a new report.</param>
public record ConditionResponse(
    int Id,
    string Name,
    string? CdcCode,
    bool Active);
