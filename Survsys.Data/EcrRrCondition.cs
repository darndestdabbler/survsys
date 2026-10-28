namespace Survsys.Data;

/// <summary>
/// One condition of a reportability response kept at intake: one this
/// jurisdiction is the responsible agency for, determined reportable or may
/// be reportable.
/// </summary>
/// <remarks>
/// The catalogue condition is not stored here: the code is looked up in
/// <see cref="EcrConditionMapping"/> when it is read, so mapping a code later
/// applies to the conditions already kept.
/// </remarks>
public class EcrRrCondition
{
    public int Id { get; set; }

    /// <summary>The RR the condition was read from.</summary>
    public int EcrDocumentId { get; set; }

    public EcrDocument? EcrDocument { get; set; }

    /// <summary>The condition code's system OID, or null where the RR gave none.</summary>
    public string? CodeSystem { get; set; }

    /// <summary>The condition code, or null where the RR gave none.</summary>
    public string? Code { get; set; }

    /// <summary>The condition's name, as the RR gives it, or null.</summary>
    public string? DisplayName { get; set; }

    /// <summary>The determination of reportability, RRVS1 or RRVS2.</summary>
    public string DeterminationCode { get; set; } = string.Empty;

    /// <summary>The responsible agency's id, its root and extension joined by a caret.</summary>
    public string? ResponsibleAgencyId { get; set; }

    /// <summary>The reporting timeframe, a value and its unit, or null.</summary>
    public string? ReportingTimeframe { get; set; }
}
