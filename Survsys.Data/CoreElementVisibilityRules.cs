namespace Survsys.Data;

/// <summary>
/// The core element visibility rule: whether a field of a case morbidity
/// report is hidden for the report's condition and agency.
/// </summary>
/// <remarks>
/// The legacy's <c>deriveDataViewLevel</c> in <c>PersonConditionController</c>
/// starts from the element's default, lets a condition override replace it,
/// and lets an agency override replace that. This keeps that order and makes
/// the one thing the legacy leaves to row order explicit: an override for the
/// agency and the condition together is the most specific, so it beats an
/// override for the agency alone. Pure, and public and static in the shape
/// <see cref="ZipAgencyRules"/> has, because the unit suite proves it without
/// a database.
/// </remarks>
public static class CoreElementVisibilityRules
{
    /// <summary>
    /// Whether <paramref name="element"/> is hidden on a report for
    /// <paramref name="conditionId"/> investigated by
    /// <paramref name="agencyId"/>, given the overrides that exist.
    /// </summary>
    /// <param name="element">The element, whose default is the answer where no override applies.</param>
    /// <param name="conditionId">The report's condition.</param>
    /// <param name="agencyId">The report's investigating agency, or null where it has none.</param>
    /// <param name="overrides">Overrides; those for another element, condition or agency are ignored.</param>
    public static bool IsHidden(
        CoreElement element, int conditionId, int? agencyId, IEnumerable<CoreElementOverride> overrides)
    {
        var applying = overrides.Where(o => o.CoreElementId == element.Id).ToList();

        var winner =
            applying.FirstOrDefault(o => agencyId is not null && o.AgencyId == agencyId && o.ConditionId == conditionId)
            ?? applying.FirstOrDefault(o => agencyId is not null && o.AgencyId == agencyId && o.ConditionId is null)
            ?? applying.FirstOrDefault(o => o.AgencyId is null && o.ConditionId == conditionId);

        return winner?.Hidden ?? element.Hidden;
    }
}
