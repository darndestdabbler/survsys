using System.Globalization;

namespace Survsys.Messaging;

/// <summary>
/// Which organisms, or which test types, a condition's notification carries
/// labs for: the condition's default set and its overrides, an item added to
/// the set (true) or taken out of it (false).
/// </summary>
/// <remarks>
/// The legacy override system described in
/// <c>../nedss/docs/business-logic/NMI_EXPORT_PIPELINE.md</c>, <i>Override
/// System</i>. A rule with no defaults and no inclusions limits nothing, so a
/// condition nobody has configured carries every lab, as it did before.
/// </remarks>
/// <param name="Defaults">The items the condition carries by default.</param>
/// <param name="Overrides">Each overridden item, and whether it is included.</param>
public sealed record LabItemRule(IReadOnlySet<int> Defaults, IReadOnlyDictionary<int, bool> Overrides)
{
    /// <summary>A rule that allows every item.</summary>
    public static readonly LabItemRule None = new(new HashSet<int>(), new Dictionary<int, bool>());

    /// <summary>Whether a lab naming <paramref name="itemId"/> is carried; a lab naming none is.</summary>
    public bool Allows(int? itemId)
    {
        if (itemId is not { } id)
        {
            return true;
        }

        if (Overrides.TryGetValue(id, out var include))
        {
            return include;
        }

        return (Defaults.Count == 0 && !Overrides.Values.Contains(true)) || Defaults.Contains(id);
    }
}

/// <summary>
/// Narrows a case's Lab dataset to the rows its condition's notification
/// carries: a row is kept only where both its organism and its test type are
/// allowed.
/// </summary>
public static class LabOverrides
{
    /// <summary>The Lab dataset field naming a row's organism.</summary>
    public const string OrganismField = "organism_id";

    /// <summary>The Lab dataset field naming a row's test type.</summary>
    public const string TestTypeField = "test_type_id";

    /// <summary>The rows of <paramref name="labs"/> both rules allow, in their order.</summary>
    public static IReadOnlyList<DatasetRow> Apply(
        IReadOnlyList<DatasetRow> labs, LabItemRule organisms, LabItemRule testTypes) =>
        labs.Where(row => organisms.Allows(Id(row, OrganismField)) && testTypes.Allows(Id(row, TestTypeField)))
            .ToList();

    internal static int? Id(DatasetRow row, string field) =>
        int.TryParse(row.Value(field), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;
}
