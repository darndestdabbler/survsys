using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="ReconciliationCutoffSetting"/> onto the
/// <c>nmi_reconciliation_cutoff</c> table, under the legacy NMI column names.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly.
/// </remarks>
internal sealed class ReconciliationCutoffSettingConfiguration
    : IEntityTypeConfiguration<ReconciliationCutoffSetting>
{
    public void Configure(EntityTypeBuilder<ReconciliationCutoffSetting> builder)
    {
        builder.ToTable("nmi_reconciliation_cutoff");
        builder.HasKey(cutoff => cutoff.Id);

        builder.Property(cutoff => cutoff.Id).HasColumnName("id");
        builder.Property(cutoff => cutoff.MmwrYear).HasColumnName("mmwr_year");
        builder.Property(cutoff => cutoff.MmwrWeek).HasColumnName("mmwr_week");
    }
}
