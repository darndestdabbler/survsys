namespace Survsys.Data;

/// <summary>
/// A lab's local LOINC code and the master LOINC it resolves to: the
/// lab-specific override the matcher reads before it reads a code as a master
/// LOINC directly.
/// </summary>
/// <remarks>
/// The columns are EpiTrax's own <c>vocab_child_loinc</c> table, as described
/// in <c>../nedss/docs/business-logic/EMSA_VOCABULARY_SYSTEM.md</c>, kept to
/// what matching needs: the lab's local code, the lab that reports it, the
/// master LOINC it maps to, the lab's own concept name, and the soft-delete
/// flag. A real ARUP example is in <c>EMSA_RULES_ENGINE.md</c>: child LOINC
/// <c>43304-5</c> for the ARUP lab resolves to the canonical master LOINC.
///
/// <see cref="MasterLoincId"/> is a bare id into <c>vocab_master_loinc</c> and
/// <see cref="LabId"/> the sending facility's id, a bare id into
/// <c>facilities</c> — the facility whose short name is a message's MSH-4.1 —
/// each a plain int and not a navigation, as the existing reference entities
/// are. There is no behaviour here and no navigation out.
/// </remarks>
public class VocabChildLoinc
{
    public int Id { get; set; }

    /// <summary>The lab's local LOINC or test code, as that lab's message carries it.</summary>
    public required string ChildLoinc { get; set; }

    /// <summary>The master LOINC this resolves to, as a bare id into <c>vocab_master_loinc</c>.</summary>
    public int MasterLoincId { get; set; }

    /// <summary>The sending facility that reports the code, as a bare id into <c>facilities</c>.</summary>
    public int LabId { get; set; }

    /// <summary>The lab's own concept name for the code, where it has one.</summary>
    public string? ConceptName { get; set; }

    /// <summary>Whether the mapping is soft-deleted and no longer resolves.</summary>
    public bool Archived { get; set; }
}
