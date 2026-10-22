namespace Survsys.Data;

/// <summary>
/// A guide's own value for a header variable, used in place of the
/// variable's <see cref="GuideVariableDefault"/> when the guide's message is
/// built.
/// </summary>
/// <remarks>
/// The legacy NMI <c>guide_variable_guide</c> table, as described in
/// <c>../nedss/docs/functional/nmi-ui-overview.md</c>. Keyed by the guide and
/// the default, so a guide sets a variable once. <see cref="GuideId"/> and
/// <see cref="GuideVariableDefaultId"/> are bare ids. There is no behaviour
/// here and no navigation out.
/// </remarks>
public class GuideVariableValue
{
    /// <summary>The guide, as a bare id into <c>guide</c>.</summary>
    public int GuideId { get; set; }

    /// <summary>The variable, as a bare id into <c>nmi_guide_variable_default</c>.</summary>
    public int GuideVariableDefaultId { get; set; }

    /// <summary>The guide's value, where set.</summary>
    public string? Value { get; set; }
}
