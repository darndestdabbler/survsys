namespace Survsys.Data;

/// <summary>
/// The MMWR week and year that bound CDC reconciliation: the boundary a case
/// morbidity report's MMWR position is weighed against to decide whether it is
/// counted in the verification totals sent to CDC.
/// </summary>
/// <remarks>
/// The legacy <c>nmi.reconciliation_cutoff</c>, as
/// <c>../nedss/docs/business-logic/NMI_EXPORT_PIPELINE.md</c> records it: a
/// single boundary of an MMWR year and week past which records are counted in
/// the verification totals CDC reconciles against. The rule there is that
/// <em>only records after this cutoff are counted</em>, so the boundary is
/// exclusive — a case reported in the cutoff week itself is not counted, and
/// counting begins with the week after.
///
/// The struct is both the rule and its answer, the shape <see cref="MmwrWeek"/>
/// has: <see cref="Counts"/> weighs an MMWR position against the boundary and
/// is pure, holding no clock and no <c>DbContext</c>. The cutoff is the
/// caller's to supply — this session persists none and reads none — and here
/// is only the comparison over it.
/// </remarks>
/// <param name="Year">The MMWR year of the boundary.</param>
/// <param name="Week">
/// The MMWR week of the boundary, within <paramref name="Year"/>. The boundary
/// is exclusive, so this week is the last one not counted.
/// </param>
public readonly record struct ReconciliationCutoff(int Year, int Week)
{
    /// <summary>
    /// Whether a case reported at <paramref name="position"/> falls within the
    /// reconciliation window — strictly after this cutoff, and so counted in
    /// the verification totals.
    /// </summary>
    /// <remarks>
    /// The MMWR year is weighed before the week, as reconciliation counts a
    /// whole year of weeks: a position in a later year counts whatever its
    /// week, a position in an earlier year never counts, and within the cutoff
    /// year a position counts only in a week after the cutoff week. A position
    /// equal to the cutoff — the same year and the same week — does not count,
    /// because the boundary is exclusive.
    /// </remarks>
    public bool Counts(MmwrWeek position) =>
        position.Year != Year ? position.Year > Year : position.Week > Week;
}
