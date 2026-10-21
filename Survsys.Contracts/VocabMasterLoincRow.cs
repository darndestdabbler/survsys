namespace Survsys.Contracts;

/// <summary>
/// A master LOINC of the ELR vocabulary and what it maps to, as
/// <c>/api/vocabulary/master-loincs</c> lists, saves and carries in CSV.
/// </summary>
/// <param name="Id">The entry's identifier; ignored when one is created.</param>
/// <param name="Loinc">The LOINC code, as a message carries it in OBR-4 or OBX-3.</param>
/// <param name="ConceptName">The preferred concept name, or null.</param>
/// <param name="ConditionId">The condition it maps to, as <c>GET /api/conditions</c> lists it, or null.</param>
/// <param name="OrganismId">The master organism it maps to, or null.</param>
/// <param name="OrganismFromResult">Whether the organism is taken from the result's SNOMED instead.</param>
public record VocabMasterLoincRow(
    int Id,
    string Loinc,
    string? ConceptName,
    int? ConditionId,
    int? OrganismId,
    bool OrganismFromResult);
