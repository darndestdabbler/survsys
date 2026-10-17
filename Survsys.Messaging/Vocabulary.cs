namespace Survsys.Messaging;

/// <summary>
/// The vocabulary the <see cref="VocabularyMatcher"/> reads, as plain in-memory
/// data: the master and child LOINC mappings and the master organism mappings,
/// gathered in one container the caller fills from whatever source it has.
/// </summary>
/// <remarks>
/// None of these is a Survsys.Data entity, and this module reaches for no
/// DbContext: the matcher takes the vocabulary as data so the messaging module
/// keeps its fence against the database, the fence the architecture test
/// asserts. Projecting the Survsys.Data reference tables into these records is
/// the caller's to do, in a tier that may see both.
/// </remarks>
/// <param name="MasterLoincs">The master LOINC entries, a LOINC to its mappings.</param>
/// <param name="ChildLoincs">The child LOINC entries, a lab's local code to a master LOINC.</param>
/// <param name="MasterOrganisms">The master organism entries, a SNOMED to its organism.</param>
public sealed record Vocabulary(
    IReadOnlyCollection<MasterLoincEntry> MasterLoincs,
    IReadOnlyCollection<ChildLoincEntry> ChildLoincs,
    IReadOnlyCollection<MasterOrganismEntry> MasterOrganisms);

/// <summary>
/// A master LOINC and what it maps to: the condition and organism the code
/// names, and whether the organism is taken from the result's SNOMED rather
/// than from the LOINC.
/// </summary>
/// <param name="Loinc">The LOINC code.</param>
/// <param name="ConditionId">The mapped condition, or null where the code names none.</param>
/// <param name="OrganismId">The mapped organism, or null where the code names none.</param>
/// <param name="OrganismFromResult">Whether the organism is derived from the result's SNOMED.</param>
public sealed record MasterLoincEntry(
    string Loinc,
    int? ConditionId,
    int? OrganismId,
    bool OrganismFromResult);

/// <summary>
/// A lab-specific child LOINC: the lab's local code and the master LOINC it
/// resolves to for that reporting lab.
/// </summary>
/// <param name="ChildLoinc">The lab's local LOINC or test code.</param>
/// <param name="LabId">The reporting lab the code belongs to.</param>
/// <param name="MasterLoinc">The master LOINC code this resolves to.</param>
public sealed record ChildLoincEntry(
    string ChildLoinc,
    int LabId,
    string MasterLoinc);

/// <summary>
/// A master organism keyed by its SNOMED code: the organism the code names and
/// the condition that organism reports.
/// </summary>
/// <param name="Snomed">The SNOMED code.</param>
/// <param name="OrganismId">The organism the code names.</param>
/// <param name="ConditionId">The condition the organism reports, or null where it maps to none.</param>
public sealed record MasterOrganismEntry(
    string Snomed,
    int? OrganismId,
    int? ConditionId);
