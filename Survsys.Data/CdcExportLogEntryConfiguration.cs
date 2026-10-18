using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="CdcExportLogEntry"/> onto the <c>nmi_log</c> table, under the
/// legacy NMI column names where there is one.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. No
/// navigation on either side: deleting the case cascades to its log rows, the
/// export history being a detail of the case. The record number and the
/// outcome code are short and bounded; the generated message and the detail
/// are unbounded text, each as long as it is.
/// </remarks>
internal sealed class CdcExportLogEntryConfiguration
    : IEntityTypeConfiguration<CdcExportLogEntry>
{
    private const int RecordNumberLength = 20;
    private const int OutcomeLength = 20;

    public void Configure(EntityTypeBuilder<CdcExportLogEntry> builder)
    {
        builder.ToTable("nmi_log");
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id).HasColumnName("id");
        builder.Property(entry => entry.PersonConditionId).HasColumnName("person_condition_id");
        builder.Property(entry => entry.RecordNumber)
            .HasColumnName("record_number")
            .HasMaxLength(RecordNumberLength)
            .IsRequired();
        builder.Property(entry => entry.GuideId).HasColumnName("guide_id");
        builder.Property(entry => entry.GeneratedMessage).HasColumnName("xml");
        builder.Property(entry => entry.QueuedAt).HasColumnName("queue_time");
        builder.Property(entry => entry.SentAt).HasColumnName("sent_at");
        builder.Property(entry => entry.Outcome)
            .HasColumnName("outcome")
            .HasMaxLength(OutcomeLength);
        builder.Property(entry => entry.Detail).HasColumnName("detail");

        builder.HasOne<PersonCondition>()
            .WithMany()
            .HasForeignKey(entry => entry.PersonConditionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
