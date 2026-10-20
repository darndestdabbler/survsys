using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="OutbreakUpdate"/> onto the <c>outbreak_updates</c> table, indexed
/// by outbreak and time, which is how the activity log is read.
/// </summary>
/// <remarks>
/// Internal, as every configuration here is. The outbreak cascades — the log
/// goes with the outbreak it is of — while the author is refused deletion with
/// <see cref="DeleteBehavior.Restrict"/>, as <see cref="AccessRecordConfiguration"/>
/// keeps its author: a user is deactivated rather than deleted. The index on
/// the outbreak and the time is what lets the log be read newest first for one
/// outbreak; the description is free text and left unbounded as
/// <see cref="NoteConfiguration"/> leaves a note's.
/// </remarks>
internal sealed class OutbreakUpdateConfiguration : IEntityTypeConfiguration<OutbreakUpdate>
{
    public void Configure(EntityTypeBuilder<OutbreakUpdate> builder)
    {
        builder.ToTable("outbreak_updates");
        builder.HasKey(update => update.Id);

        builder.Property(update => update.Id).HasColumnName("id");
        builder.Property(update => update.OutbreakId).HasColumnName("outbreak_id");
        builder.Property(update => update.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        builder.Property(update => update.UserId).HasColumnName("user_id");
        builder.Property(update => update.Description).HasColumnName("description");
        builder.Property(update => update.Type)
            .HasColumnName("type")
            .IsRequired();

        builder.HasIndex(update => new { update.OutbreakId, update.CreatedAt });

        builder.HasOne<Outbreak>()
            .WithMany()
            .HasForeignKey(update => update.OutbreakId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(update => update.UserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
