namespace Survsys.Data;

/// <summary>
/// A lab's own code for a specimen source, a test result or an organism, and
/// the master it resolves to for that lab: the child tier, the lab-specific
/// override read before a code is looked up as a master directly.
/// </summary>
/// <remarks>
/// The legacy <c>vocab_child_vocab</c> and <c>vocab_child_organism</c> tables,
/// as described in <c>../nedss/docs/business-logic/EMSA_VOCABULARY_SYSTEM.md</c>,
/// in one table keyed by category: for <c>specimen</c> and <c>test_result</c>
/// <see cref="MasterId"/> is a bare id into <c>vocab_master_vocab</c>
/// (<see cref="VocabMasterValue"/>), and for <c>organism</c> a bare id into
/// <c>vocab_master_organism</c> (<see cref="VocabMasterOrganism"/>).
/// <see cref="LabId"/> is the sending facility's id, as it is on
/// <see cref="VocabChildLoinc"/>. There is no behaviour here and no navigation
/// out.
/// </remarks>
public class VocabChildValue
{
    public int Id { get; set; }

    /// <summary>The sending facility the code belongs to, as a bare id into <c>facilities</c>.</summary>
    public int LabId { get; set; }

    /// <summary>The category: <c>specimen</c>, <c>test_result</c> or <c>organism</c>.</summary>
    public required string Category { get; set; }

    /// <summary>The lab's own code, as its message carries it.</summary>
    public required string ChildCode { get; set; }

    /// <summary>
    /// The master this resolves to: an id into <c>vocab_master_vocab</c>, or
    /// into <c>vocab_master_organism</c> for the <c>organism</c> category.
    /// </summary>
    public int MasterId { get; set; }
}
