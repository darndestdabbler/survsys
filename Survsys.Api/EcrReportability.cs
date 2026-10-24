using Survsys.Data;
using Survsys.Messaging;

namespace Survsys.Api;

/// <summary>
/// What a reportability response asks of this jurisdiction: which of its
/// conditions are kept, and where a kept condition goes.
/// </summary>
/// <remarks>
/// The determinations are the HL7 eCR value set
/// (<c>2.16.840.1.114222.4.5.274</c>). A condition is kept where this
/// jurisdiction is its responsible agency and it is reportable or may be
/// reportable; one not reportable, with no rule met, with no determination,
/// or for another agency is not this jurisdiction's to act on. A kept
/// condition is reportable where it is determined reportable and its code
/// maps to a catalogue condition, and goes to review otherwise.
/// </remarks>
public static class EcrReportability
{
    /// <summary>Reportable.</summary>
    public const string Reportable = "RRVS1";

    /// <summary>May be reportable.</summary>
    public const string MayBeReportable = "RRVS2";

    /// <summary>Not reportable.</summary>
    public const string NotReportable = "RRVS3";

    /// <summary>No rule met.</summary>
    public const string NoRuleMet = "RRVS4";

    /// <summary>The routing of a kept condition that is reportable and mapped.</summary>
    public const string ReportableRouting = "reportable";

    /// <summary>The routing of a kept condition a reviewer must look at.</summary>
    public const string ReviewRouting = "review";

    /// <summary>
    /// Whether <paramref name="condition"/> is this jurisdiction's to act on:
    /// its responsible agency is one of <paramref name="responsibleAgencyIds"/>
    /// and it is reportable or may be reportable.
    /// </summary>
    public static bool IsKept(RrCondition condition, IReadOnlyCollection<string> responsibleAgencyIds)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(responsibleAgencyIds);

        return condition.ResponsibleAgencyId is { } agency
            && responsibleAgencyIds.Contains(agency, StringComparer.OrdinalIgnoreCase)
            && condition.Determination?.Code is Reportable or MayBeReportable;
    }

    /// <summary>
    /// Where a kept condition goes: <see cref="ReportableRouting"/> where it is
    /// <see cref="Reportable"/> and <paramref name="mapped"/>, and
    /// <see cref="ReviewRouting"/> otherwise.
    /// </summary>
    public static string Route(string? determinationCode, bool mapped) =>
        determinationCode == Reportable && mapped ? ReportableRouting : ReviewRouting;

    /// <summary>The conditions of <paramref name="rr"/> kept, as the rows stored with it.</summary>
    public static List<EcrRrCondition> Kept(CdaDocument rr, IReadOnlyCollection<string> responsibleAgencyIds)
    {
        ArgumentNullException.ThrowIfNull(rr);

        return rr.Conditions
            .Where(condition => IsKept(condition, responsibleAgencyIds))
            .Select(condition => new EcrRrCondition
            {
                CodeSystem = condition.Condition?.CodeSystem,
                Code = condition.Condition?.Code,
                DisplayName = condition.Condition?.DisplayName,
                DeterminationCode = condition.Determination!.Code!,
                ResponsibleAgencyId = condition.ResponsibleAgencyId,
                ReportingTimeframe = condition.ReportingTimeframe,
            })
            .ToList();
    }
}
