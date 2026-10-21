namespace Survsys.Data;

/// <summary>
/// A master organism keyed by its SNOMED code: the entry the matcher reads to
/// turn the SNOMED a result carries into an organism and the condition that
/// organism reports.
/// </summary>
/// <remarks>
/// The columns are EpiTrax's own <c>vocab_master_organism</c> table, as
/// described in <c>../nedss/docs/business-logic/EMSA_VOCABULARY_SYSTEM.md</c>,
/// kept to what matching needs: the primary and alternate SNOMED codes, the
/// organism's name, and the condition it maps to.
///
/// <see cref="ConditionId"/> is a bare nullable id into <c>conditions</c>,
/// held as a plain int and not a navigation, as the existing reference
/// entities are. The organism itself is this row — its <see cref="Id"/> is the
/// legacy <c>o_id</c> a result's organism is known by — so the name is carried
/// here rather than reached through a further lookup. There is no behaviour
/// here and no navigation out.
/// </remarks>
public class VocabMasterOrganism
{
    public int Id { get; set; }

    /// <summary>The primary SNOMED code, as a result carries it in OBX-5.</summary>
    public required string Snomed { get; set; }

    /// <summary>An alternate SNOMED code for the same organism, where it has one.</summary>
    public string? SnomedAlt { get; set; }

    /// <summary>The organism's name, as a screen shows it.</summary>
    public required string OrganismName { get; set; }

    /// <summary>
    /// The condition this organism reports, as a bare id into <c>conditions</c>;
    /// null where the organism maps to no condition on its own.
    /// </summary>
    public int? ConditionId { get; set; }
}
