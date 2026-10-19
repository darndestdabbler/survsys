using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="NotificationQueueEntry"/> onto the <c>nmi_export_queue</c> table,
/// under the legacy NMI column names where there is one.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. The
/// foreign key to <c>person_condition</c> is unique, so a case has at most one
/// queue entry and the upsert on a case change refreshes that one rather than
/// making a second. No navigation on either side, as the reference rows have
/// none: deleting the case cascades to its queue entry, a detail of the case
/// and not a record of its own.
/// </remarks>
internal sealed class NotificationQueueEntryConfiguration
    : IEntityTypeConfiguration<NotificationQueueEntry>
{
    public void Configure(EntityTypeBuilder<NotificationQueueEntry> builder)
    {
        builder.ToTable("nmi_export_queue");
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id).HasColumnName("id");
        builder.Property(entry => entry.PersonConditionId).HasColumnName("person_condition_id");
        builder.Property(entry => entry.QueuedAt).HasColumnName("queued_at");
        builder.Property(entry => entry.Hold).HasColumnName("hold");

        builder.HasIndex(entry => entry.PersonConditionId).IsUnique();

        builder.HasOne<PersonCondition>()
            .WithMany()
            .HasForeignKey(entry => entry.PersonConditionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
