namespace Survsys.Data;

/// <summary>
/// A condition's override of which organisms or test types its notification
/// carries labs for: an item added to the condition's default set, or taken
/// out of it.
/// </summary>
/// <remarks>
/// The legacy NMI <c>override_condition_organism</c> and
/// <c>override_condition_test_type</c> tables, as described in
/// <c>../nedss/docs/business-logic/NMI_EXPORT_PIPELINE.md</c>, <i>Override
/// System</i>, held as one table told apart by <see cref="Kind"/>.
/// <see cref="ItemId"/> is a bare id into the vocabulary's master organisms or
/// the <c>test_type</c> code values. Unique on the condition, kind and item.
/// There is no behaviour here.
/// </remarks>
public class ConditionLabOverride
{
    public const string OrganismKind = "organism";
    public const string TestTypeKind = "test_type";

    /// <summary>The override's id. The key.</summary>
    public int Id { get; set; }

    /// <summary>The condition, as a bare id into <c>conditions</c>.</summary>
    public int ConditionId { get; set; }

    /// <summary>What the item is: <see cref="OrganismKind"/> or <see cref="TestTypeKind"/>.</summary>
    public required string Kind { get; set; }

    /// <summary>The organism or test type, as a bare id.</summary>
    public int ItemId { get; set; }

    /// <summary>True where the item is added to the default set, false where it is taken out.</summary>
    public bool Include { get; set; }
}
