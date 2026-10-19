namespace Survsys.Messaging;

/// <summary>
/// The vocabulary the <see cref="VocabularyMatcher"/> reads, as plain in-memory
/// data: the master and child LOINC mappings, the master organism mappings,
/// and the generic master values and lab-specific child codes, gathered in one
/// container the caller fills from whatever source it has.
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
/// <param name="MasterValues">The master values, a specimen or test-result concept to its application value.</param>
/// <param name="ChildValues">The child codes, a lab's own specimen, result or organism code to its master.</param>
public sealed record Vocabulary(
    IReadOnlyCollection<MasterLoincEntry> MasterLoincs,
    IReadOnlyCollection<ChildLoincEntry> ChildLoincs,
    IReadOnlyCollection<MasterOrganismEntry> MasterOrganisms,
    IReadOnlyCollection<MasterValueEntry> MasterValues,
    IReadOnlyCollection<ChildValueEntry> ChildValues);

/// <summary>
/// The categories a <see cref="MasterValueEntry"/> and a
/// <see cref="ChildValueEntry"/> are kept under, as the codes they are stored as.
/// </summary>
public static class VocabularyCategory
{
    /// <summary>A specimen source.</summary>
    public const string Specimen = "specimen";

    /// <summary>A test result, whose application value is the result's interpretation.</summary>
    public const string TestResult = "test_result";

    /// <summary>
    /// An organism: a child code in this category resolves to a master
    /// organism rather than to a master value.
    /// </summary>
    public const string Organism = "organism";
}

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
/// <param name="SnomedAlt">An alternate SNOMED code for the same organism, where it has one.</param>
/// <param name="OrganismName">The organism's name, where known.</param>
public sealed record MasterOrganismEntry(
    string Snomed,
    int? OrganismId,
    int? ConditionId,
    string? SnomedAlt = null,
    string? OrganismName = null);

/// <summary>
/// A master value: a specimen or test-result concept and the value the
/// application records for it.
/// </summary>
/// <param name="Id">The master value's id, the one a child code names.</param>
/// <param name="Category">The <see cref="VocabularyCategory"/> it is kept under.</param>
/// <param name="Concept">The master concept.</param>
/// <param name="AppValue">The application value, or null where it has none.</param>
public sealed record MasterValueEntry(
    int Id,
    string Category,
    string Concept,
    string? AppValue);

/// <summary>
/// A lab's own code for a specimen, a test result or an organism, and the
/// master it resolves to for that lab.
/// </summary>
/// <param name="LabId">The reporting lab the code belongs to.</param>
/// <param name="Category">The <see cref="VocabularyCategory"/> it is kept under.</param>
/// <param name="ChildCode">The lab's own code.</param>
/// <param name="MasterId">
/// The master it resolves to: a <see cref="MasterValueEntry.Id"/>, or for the
/// organism category a <see cref="MasterOrganismEntry.OrganismId"/>.
/// </param>
public sealed record ChildValueEntry(
    int LabId,
    string Category,
    string ChildCode,
    int MasterId);
