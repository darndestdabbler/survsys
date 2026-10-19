namespace Survsys.Messaging;

/// <summary>
/// Resolves the PHIN variables a case is notified with: for a condition, the
/// guide it is exported under and the variables that guide carries together
/// with the variables of the guides it links. This is the mapping half of the
/// outbound CDC notification, over data rather than a database — the mapper is
/// built once over a <see cref="PhinVariableCatalog"/> and holds it as lookups,
/// and reaches for no DbContext.
/// </summary>
/// <remarks>
/// A condition is exported under one guide, and a guide pulls in the variables
/// of the guides it links one level deep, as the legacy XML generation does.
/// The result is distinct — a variable a guide and its linked guide both carry
/// appears once — and ordered by the variable identifier, so the mapping is
/// stable whatever order the catalogue was gathered in. A condition with no
/// assigned guide, or a guide that carries no variables, yields no variables
/// rather than an exception, and a membership naming a variable the catalogue
/// does not define is skipped.
/// </remarks>
public sealed class PhinVariableMapper
{
    private readonly IReadOnlyDictionary<int, int> _guideByCondition;
    private readonly ILookup<int, int> _linkedGuides;
    private readonly ILookup<int, string> _variablesByGuide;
    private readonly IReadOnlyDictionary<string, PhinVariable> _definitions;

    public PhinVariableMapper(PhinVariableCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        _guideByCondition = catalog.ConditionGuides.ToDictionary(
            assignment => assignment.ConditionId,
            assignment => assignment.GuideId);
        _linkedGuides = catalog.GuideLinks.ToLookup(
            link => link.GuideId,
            link => link.LinkedGuideId);
        _variablesByGuide = catalog.GuideVariables.ToLookup(
            membership => membership.GuideId,
            membership => membership.PhinVar);
        _definitions = catalog.Variables.ToDictionary(
            variable => variable.PhinVar, StringComparer.Ordinal);
    }

    /// <summary>
    /// The PHIN variables a case of the given condition is notified with: the
    /// variables of the guide the condition is exported under, together with
    /// the variables of the guides that guide links, resolved to their
    /// definitions, distinct and ordered by the variable identifier. A
    /// condition with no assigned guide yields an empty result.
    /// </summary>
    public IReadOnlyList<PhinVariable> MapForCondition(int conditionId)
    {
        if (!_guideByCondition.TryGetValue(conditionId, out var guideId))
        {
            return [];
        }

        var guides = new HashSet<int> { guideId };
        foreach (var linked in _linkedGuides[guideId])
        {
            guides.Add(linked);
        }

        return guides
            .SelectMany(guide => _variablesByGuide[guide])
            .Distinct(StringComparer.Ordinal)
            .Where(phinVar => _definitions.ContainsKey(phinVar))
            .OrderBy(phinVar => phinVar, StringComparer.Ordinal)
            .Select(phinVar => _definitions[phinVar])
            .ToList();
    }
}
