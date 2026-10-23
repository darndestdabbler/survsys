namespace Survsys.Data;

/// <summary>
/// One case an eICR version was written to: the version and the case, and
/// the values and rows it wrote there.
/// </summary>
/// <remarks>
/// The link is to the version, not the set, so each value an eICR writes
/// records the eICR id and version it came from through its link. A later
/// version of a set goes to the cases its earlier version is linked to.
/// </remarks>
public class EcrCaseLink
{
    public int Id { get; set; }

    /// <summary>The eICR version written.</summary>
    public int EcrDocumentId { get; set; }

    public EcrDocument? EcrDocument { get; set; }

    /// <summary>The case it was written to.</summary>
    public int PersonConditionId { get; set; }

    /// <summary>When the version was written to the case.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>What the version wrote on the case.</summary>
    public List<EcrCaseValue> Values { get; set; } = [];
}
