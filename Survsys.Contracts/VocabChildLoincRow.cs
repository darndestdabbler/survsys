namespace Survsys.Contracts;

/// <summary>
/// A sending lab's own LOINC or test code and the master LOINC it resolves to
/// for that lab: the lab-specific override, as
/// <c>/api/vocabulary/child-loincs</c> lists, saves and carries in CSV.
/// </summary>
/// <param name="Id">The entry's identifier; ignored when one is created.</param>
/// <param name="LabId">The sending facility, as <c>GET /api/facilities</c> lists it.</param>
/// <param name="ChildLoinc">The lab's own code.</param>
/// <param name="MasterLoinc">The master LOINC code it resolves to, one on file.</param>
/// <param name="ConceptName">The lab's own concept name, or null.</param>
/// <param name="Archived">Whether the mapping is retired and no longer resolves.</param>
public record VocabChildLoincRow(
    int Id,
    int LabId,
    string ChildLoinc,
    string MasterLoinc,
    string? ConceptName,
    bool Archived);
