namespace Survsys.Contracts;

/// <summary>
/// A master organism of the ELR vocabulary, keyed by its SNOMED code, as
/// <c>/api/vocabulary/organisms</c> lists, saves and carries in CSV.
/// </summary>
/// <param name="Id">The organism's identifier; ignored when one is created.</param>
/// <param name="Snomed">The primary SNOMED code.</param>
/// <param name="SnomedAlt">An alternate SNOMED code, or null.</param>
/// <param name="OrganismName">The organism's name.</param>
/// <param name="ConditionId">The condition the organism reports, or null.</param>
public record VocabMasterOrganismRow(
    int Id,
    string Snomed,
    string? SnomedAlt,
    string OrganismName,
    int? ConditionId);
