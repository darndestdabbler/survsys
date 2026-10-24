namespace Survsys.Data;

/// <summary>
/// One condition code a reportability response can carry, an RCTC condition
/// grouper or a SNOMED condition code, and the condition in the catalogue it
/// stands for.
/// </summary>
/// <remarks>
/// A code is chosen by its code system and code together, so that pair is
/// unique. An RCTC release's condition list is imported with
/// <see cref="ConditionId"/> left null, and a code stays unmapped until
/// someone chooses its condition on the mapping screen.
/// </remarks>
public class EcrConditionMapping
{
    public int Id { get; set; }

    /// <summary>The code system's OID, such as SNOMED CT's <c>2.16.840.1.113883.6.96</c>.</summary>
    public string CodeSystem { get; set; } = string.Empty;

    /// <summary>The condition code within <see cref="CodeSystem"/>.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>The code's name, as the release or the RR gives it, or null.</summary>
    public string? DisplayName { get; set; }

    /// <summary>The catalogue condition the code maps to, as a bare id into <c>conditions</c>; null while unmapped.</summary>
    public int? ConditionId { get; set; }
}
