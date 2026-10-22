using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="HepLabGroup"/> onto the <c>nmi_hep_lab_group</c> table.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. Unique on
/// the name.
/// </remarks>
internal sealed class HepLabGroupConfiguration : IEntityTypeConfiguration<HepLabGroup>
{
    public void Configure(EntityTypeBuilder<HepLabGroup> builder)
    {
        builder.ToTable("nmi_hep_lab_group");
        builder.HasKey(group => group.Id);

        builder.Property(group => group.Id).HasColumnName("id");
        builder.Property(group => group.Name)
            .HasColumnName("group_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(group => group.Name).IsUnique();
    }
}
