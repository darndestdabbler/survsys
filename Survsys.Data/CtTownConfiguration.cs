using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="CtTown"/> onto the <c>ct_towns</c> table, seeded with
/// <see cref="CtTownSeed"/>.
/// </summary>
internal sealed class CtTownConfiguration : IEntityTypeConfiguration<CtTown>
{
    public void Configure(EntityTypeBuilder<CtTown> builder)
    {
        builder.ToTable("ct_towns");
        builder.HasKey(town => town.Town);

        builder.Property(town => town.Town)
            .HasColumnName("town")
            .HasMaxLength(50);
        builder.Property(town => town.County)
            .HasColumnName("county")
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(town => town.PlanningRegion)
            .HasColumnName("planning_region")
            .HasMaxLength(50)
            .IsRequired();

        builder.HasData(CtTownSeed.Rows);
    }
}
