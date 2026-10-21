namespace Survsys.Data;

/// <summary>
/// An EpiTrax field a ruleset configuration references: one field name the
/// configuration's transformation reads a value from.
/// </summary>
/// <remarks>
/// The columns are the legacy NMI <c>rule_set_field</c> table, as described in
/// <c>../nedss/docs/business-logic/NMI_RULESETS.md</c>: the configuration it
/// belongs to and the field name. <see cref="RuleSetConfigId"/> is a bare id
/// into <c>rule_set_config</c>, not a navigation, as the existing reference
/// entities are. There is no behaviour here and no navigation out.
/// </remarks>
public class RuleSetField
{
    /// <summary>The field's id. The key.</summary>
    public int Id { get; set; }

    /// <summary>
    /// The configuration this field belongs to, as a bare id into
    /// <c>rule_set_config</c>.
    /// </summary>
    public int RuleSetConfigId { get; set; }

    /// <summary>The EpiTrax field name the value is read from.</summary>
    public required string Field { get; set; }
}
