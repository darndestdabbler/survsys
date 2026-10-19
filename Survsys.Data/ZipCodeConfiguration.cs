using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

internal sealed class ZipCodeConfiguration : IEntityTypeConfiguration<ZipCode>
{
    public void Configure(EntityTypeBuilder<ZipCode> builder)
    {
        builder.ToTable("zip_codes");
        builder.HasKey(zip => zip.Code);

        builder.Property(zip => zip.Code)
            .HasColumnName("code")
            .HasMaxLength(ZipAgencyRules.ZipLength)
            .IsRequired();
        builder.Property(zip => zip.County)
            .HasColumnName("county")
            .HasMaxLength(50);
    }
}
