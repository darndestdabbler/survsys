using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="GuidePhinVariable"/> onto the <c>guide_phin_var</c> table,
/// column by column and under the legacy names.
/// </summary>
/// <remarks>
/// Internal, because nothing outside this module may say what the schema is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. Keyed by
/// the guide-and-variable pair, so a guide carries a variable once.
/// </remarks>
internal sealed class GuidePhinVariableConfiguration : IEntityTypeConfiguration<GuidePhinVariable>
{
    public void Configure(EntityTypeBuilder<GuidePhinVariable> builder)
    {
        builder.ToTable("guide_phin_var");
        builder.HasKey(membership => new { membership.GuideId, membership.PhinVar });

        builder.Property(membership => membership.GuideId).HasColumnName("guide_id");
        builder.Property(membership => membership.PhinVar)
            .HasColumnName("phin_var")
            .HasMaxLength(50);
    }
}
