namespace Survsys.Data;

/// <summary>
/// A master LOINC code and what it maps to: the canonical entry the matcher
/// reads to turn the LOINC a lab message carries into a condition and an
/// organism.
/// </summary>
/// <remarks>
/// The columns are EpiTrax's own <c>vocab_master_loinc</c> table, as described
/// in <c>../nedss/docs/business-logic/EMSA_VOCABULARY_SYSTEM.md</c>, kept to
/// what matching a message needs: the code, its preferred concept name, the
/// condition and organism it maps to, and the flag that says the organism is
/// taken from the result's SNOMED rather than from the LOINC itself.
///
/// <see cref="ConditionId"/> and <see cref="OrganismId"/> are bare nullable
/// ids — into <c>conditions</c> and <c>vocab_master_organism</c> — held as
/// plain ints and not navigations, as the existing reference entities are
/// (<see cref="Condition.ConditionGroupId"/>). The matcher reads this table as
/// data, so there is no behaviour here and no navigation out.
///
/// The rules EMSA attaches to a master LOINC — the <c>vocab_rules_masterloinc</c>
/// case-management rules — are not here; this is the mapping, not the engine.
/// </remarks>
public class VocabMasterLoinc
{
    public int Id { get; set; }

    /// <summary>The LOINC code, as a message carries it in OBR-4 or OBX-3.</summary>
    public required string Loinc { get; set; }

    /// <summary>The preferred concept name for the code, where it has one.</summary>
    public string? ConceptName { get; set; }

    /// <summary>
    /// The condition this LOINC maps to, as a bare id into <c>conditions</c>;
    /// null where the code names no condition on its own.
    /// </summary>
    public int? ConditionId { get; set; }

    /// <summary>
    /// The master organism this LOINC maps to, as a bare id into
    /// <c>vocab_master_organism</c>; null where the code names no organism.
    /// </summary>
    public int? OrganismId { get; set; }

    /// <summary>
    /// Whether the organism is derived from the result's SNOMED code rather
    /// than from this LOINC. The legacy <c>organism_from_result</c> flag.
    /// </summary>
    public bool OrganismFromResult { get; set; }
}
