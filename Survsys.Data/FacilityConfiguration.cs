using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="Facility"/> onto the <c>facilities</c> table, under the legacy
/// column names.
/// </summary>
/// <remarks>
/// The agency is a real foreign key, and an agency with facilities under it
/// cannot be deleted out from under them: nothing deletes an agency, and if
/// something ever does it has to say where the facilities go.
/// </remarks>
internal sealed class FacilityConfiguration : IEntityTypeConfiguration<Facility>
{
    public void Configure(EntityTypeBuilder<Facility> builder)
    {
        builder.ToTable("facilities");
        builder.HasKey(facility => facility.Id);

        builder.Property(facility => facility.Id).HasColumnName("id");
        builder.Property(facility => facility.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(facility => facility.ShortName)
            .HasColumnName("short_name")
            .HasMaxLength(50);
        builder.Property(facility => facility.AgencyId).HasColumnName("agency_id");
        builder.Property(facility => facility.Street)
            .HasColumnName("street")
            .HasMaxLength(200);
        builder.Property(facility => facility.City)
            .HasColumnName("city")
            .HasMaxLength(100);
        builder.Property(facility => facility.State)
            .HasColumnName("state")
            .HasMaxLength(2);
        builder.Property(facility => facility.Zip)
            .HasColumnName("zip")
            .HasMaxLength(ZipAgencyRules.ZipLength);
        builder.Property(facility => facility.Phone)
            .HasColumnName("phone")
            .HasMaxLength(30);

        builder.HasOne<Agency>()
            .WithMany()
            .HasForeignKey(facility => facility.AgencyId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
