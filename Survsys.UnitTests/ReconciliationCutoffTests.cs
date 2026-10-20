using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The reconciliation cutoff boundary: which MMWR positions a case is counted
/// in, weighed against a cutoff of MMWR year 2024 week 30.
/// </summary>
/// <remarks>
/// Pure throughout, so every case here is the comparison itself and not a
/// database or a host. The boundary is the legacy
/// <c>nmi.reconciliation_cutoff</c>'s, as
/// <c>../nedss/docs/business-logic/NMI_EXPORT_PIPELINE.md</c> records it: only
/// records after the cutoff are counted in the verification totals CDC
/// reconciles against, so the boundary is exclusive — the cutoff week itself
/// is not counted — and the MMWR year is weighed before the week.
/// </remarks>
public class ReconciliationCutoffTests
{
    private static readonly ReconciliationCutoff Cutoff = new(2024, 30);

    /// <summary>
    /// A position in the cutoff year but an earlier week is before the
    /// boundary and is not counted.
    /// </summary>
    [Fact]
    public void APositionBeforeTheCutoffWeekIsNotCounted()
    {
        Assert.False(Cutoff.Counts(new MmwrWeek(29, 2024)));
    }

    /// <summary>
    /// A position in the cutoff year but a later week is after the boundary
    /// and is counted.
    /// </summary>
    [Fact]
    public void APositionAfterTheCutoffWeekIsCounted()
    {
        Assert.True(Cutoff.Counts(new MmwrWeek(31, 2024)));
    }

    /// <summary>
    /// A position on the cutoff week itself — the same year and the same week
    /// — is not counted, because the boundary is exclusive. The boundary case
    /// the session plan calls for.
    /// </summary>
    [Fact]
    public void APositionOnTheCutoffWeekIsNotCounted()
    {
        Assert.False(Cutoff.Counts(new MmwrWeek(30, 2024)));
    }

    /// <summary>
    /// A position in a later MMWR year is counted whatever its week, because
    /// the year is weighed before the week — here week 1 of 2025, an earlier
    /// week number than the cutoff's.
    /// </summary>
    [Fact]
    public void APositionInALaterYearIsCountedEvenWithAnEarlierWeekNumber()
    {
        Assert.True(Cutoff.Counts(new MmwrWeek(1, 2025)));
    }

    /// <summary>
    /// A position in an earlier MMWR year is never counted, whatever its week
    /// — here week 52 of 2023, a later week number than the cutoff's.
    /// </summary>
    [Fact]
    public void APositionInAnEarlierYearIsNotCountedEvenWithALaterWeekNumber()
    {
        Assert.False(Cutoff.Counts(new MmwrWeek(52, 2023)));
    }
}
