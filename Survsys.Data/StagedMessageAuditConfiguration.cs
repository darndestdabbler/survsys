using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="StagedMessageAudit"/> onto the legacy
/// <c>system_messages_audits</c> table.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is. An audit row is a
/// detail of its message and cascades with it; the codes are bounded as the
/// message's own columns are, and the detail is unbounded as the message's is.
/// </remarks>
internal sealed class StagedMessageAuditConfiguration : IEntityTypeConfiguration<StagedMessageAudit>
{
    public void Configure(EntityTypeBuilder<StagedMessageAudit> builder)
    {
        builder.ToTable("system_messages_audits");
        builder.HasKey(audit => audit.Id);

        builder.Property(audit => audit.Id).HasColumnName("id");
        builder.Property(audit => audit.StagedMessageId).HasColumnName("system_message_id");
        builder.Property(audit => audit.ReviewStatus)
            .HasColumnName("final_status")
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(audit => audit.ExceptionCategory)
            .HasColumnName("exception_category")
            .HasMaxLength(40);
        builder.Property(audit => audit.ExceptionDetail).HasColumnName("exception_detail");
        builder.Property(audit => audit.OccurredAt).HasColumnName("occurred_at");

        builder.HasIndex(audit => audit.StagedMessageId);
    }
}
