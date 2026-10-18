using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="ZipCodeAgency"/> onto the <c>zip_code_agencies</c> table, keyed
/// on the pair as the legacy <c>zip_code_agency</c> is.
/// </summary>
/// <remarks>
/// The agency is a real foreign key, because <see cref="Agency"/> is a real
/// table; a mapping row goes with its agency. The zip code has no table to
/// point at — the legacy <c>zip_code</c> table carries only a county, which
/// nothing here reads.
/// </remarks>
internal sealed class ZipCodeAgencyConfiguration : IEntityTypeConfiguration<ZipCodeAgency>
{
    public void Configure(EntityTypeBuilder<ZipCodeAgency> builder)
    {
        builder.ToTable("zip_code_agencies");
        builder.HasKey(mapping => new { mapping.ZipCode, mapping.AgencyId });

        builder.Property(mapping => mapping.ZipCode)
            .HasColumnName("zip_code")
            .HasMaxLength(ZipAgencyRules.ZipLength)
            .IsRequired();
        builder.Property(mapping => mapping.AgencyId).HasColumnName("agency_id");

        builder.HasOne<Agency>()
            .WithMany()
            .HasForeignKey(mapping => mapping.AgencyId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
