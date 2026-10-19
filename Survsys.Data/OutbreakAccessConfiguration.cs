using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="OutbreakAccess"/> onto the <c>outbreak_access</c> table, keyed by
/// id and unique on the outbreak-and-agency pair.
/// </summary>
/// <remarks>
/// Internal, as every configuration here is. The outbreak is deleted with the
/// outbreak it grants on — a grant is a detail of the outbreak and goes when it
/// does — so its foreign key cascades; the agency is refused deletion with
/// <see cref="DeleteBehavior.Restrict"/>, as the owning agency on
/// <see cref="OutbreakConfiguration"/> is, because an agency is a record of its
/// own. The unique index on the pair is what makes a second grant of the same
/// agency the same row rather than another.
/// </remarks>
internal sealed class OutbreakAccessConfiguration : IEntityTypeConfiguration<OutbreakAccess>
{
    public void Configure(EntityTypeBuilder<OutbreakAccess> builder)
    {
        builder.ToTable("outbreak_access");
        builder.HasKey(access => access.Id);

        builder.Property(access => access.Id).HasColumnName("id");
        builder.Property(access => access.OutbreakId).HasColumnName("outbreak_id");
        builder.Property(access => access.AgencyId).HasColumnName("agency_id");
        builder.Property(access => access.Type)
            .HasColumnName("type")
            .IsRequired();

        builder.HasIndex(access => new { access.OutbreakId, access.AgencyId }).IsUnique();

        builder.HasOne<Outbreak>()
            .WithMany()
            .HasForeignKey(access => access.OutbreakId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Agency>()
            .WithMany()
            .HasForeignKey(access => access.AgencyId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
