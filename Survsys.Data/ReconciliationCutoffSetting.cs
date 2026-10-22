namespace Survsys.Data;

/// <summary>
/// The stored reconciliation cutoff: the one row of <c>nmi_reconciliation_cutoff</c>,
/// read as a <see cref="ReconciliationCutoff"/> through <see cref="ToCutoff"/>.
/// </summary>
/// <remarks>
/// The legacy single-row <c>nmi.reconciliation_cutoff</c>. The rule over the
/// boundary stays on the <see cref="ReconciliationCutoff"/> struct; this is only
/// where it is kept. No row means no cutoff has been set.
/// </remarks>
public class ReconciliationCutoffSetting
{
    /// <summary>The row's id. The key.</summary>
    public int Id { get; set; }

    /// <summary>The MMWR year of the boundary. The legacy <c>mmwr_year</c>.</summary>
    public int MmwrYear { get; set; }

    /// <summary>The MMWR week of the boundary, the last week not counted. The legacy <c>mmwr_week</c>.</summary>
    public int MmwrWeek { get; set; }

    /// <summary>The stored boundary as the cutoff that weighs a case against it.</summary>
    public ReconciliationCutoff ToCutoff() => new(MmwrYear, MmwrWeek);
}
