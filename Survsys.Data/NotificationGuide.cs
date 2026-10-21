namespace Survsys.Data;

/// <summary>
/// A notification guide: the CDC profile a case is exported under, naming the
/// set of PHIN variables its message carries (for example "Generic v2.0" or
/// "STD MMG v1.0").
/// </summary>
/// <remarks>
/// The columns are the legacy NMI <c>guide</c> table, as described in
/// <c>../nedss/docs/business-logic/NMI_RULESETS.md</c>, kept to what the
/// mapping needs: the guide's id and its name. Which variables a guide carries
/// is <see cref="GuidePhinVariable"/>, which guides it pulls in alongside is
/// <see cref="GuideLink"/>, and which condition is exported under it is
/// <see cref="ConditionDetail"/>; there is no behaviour here and no navigation
/// out.
/// </remarks>
public class NotificationGuide
{
    public int Id { get; set; }

    /// <summary>The guide's name, as a screen shows it.</summary>
    public required string Name { get; set; }

    /// <summary>
    /// The guide's version (for example <c>Arbo_Case_Map_v1.0</c>), the
    /// profile its message is generated for, where set. A guide is matched by
    /// its name and version when one is imported.
    /// </summary>
    public string? Version { get; set; }
}
