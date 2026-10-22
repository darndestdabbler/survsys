using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="CdcExportLogDetail"/> onto the <c>nmi_log_detail</c> table,
/// under the legacy NMI column names.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. The link
/// to its log row is configured on <see cref="CdcExportLogEntryConfiguration"/>,
/// which owns the <see cref="CdcExportLogEntry.Details"/> collection.
/// </remarks>
internal sealed class CdcExportLogDetailConfiguration
    : IEntityTypeConfiguration<CdcExportLogDetail>
{
    private const int PhinVarLength = 50;
    private const int SeverityLength = 20;

    public void Configure(EntityTypeBuilder<CdcExportLogDetail> builder)
    {
        builder.ToTable("nmi_log_detail");
        builder.HasKey(detail => detail.Id);

        builder.Property(detail => detail.Id).HasColumnName("id");
        builder.Property(detail => detail.LogId).HasColumnName("log_id");
        builder.Property(detail => detail.PhinVar)
            .HasColumnName("phin_var")
            .HasMaxLength(PhinVarLength);
        builder.Property(detail => detail.Message)
            .HasColumnName("message")
            .IsRequired();
        builder.Property(detail => detail.Severity)
            .HasColumnName("severity")
            .HasMaxLength(SeverityLength)
            .IsRequired();
    }
}
