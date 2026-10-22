using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="ConditionDetail"/> onto the <c>condition_detail</c> table,
/// column by column and under the legacy names.
/// </summary>
/// <remarks>
/// Internal, because nothing outside this module may say what the schema is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. The
/// condition is the key — one detail row per condition — so a condition cannot
/// be exported under two guides at once.
/// </remarks>
internal sealed class ConditionDetailConfiguration : IEntityTypeConfiguration<ConditionDetail>
{
    public void Configure(EntityTypeBuilder<ConditionDetail> builder)
    {
        builder.ToTable("condition_detail");
        builder.HasKey(detail => detail.ConditionId);

        builder.Property(detail => detail.ConditionId)
            .HasColumnName("condition_id")
            .ValueGeneratedNever();
        builder.Property(detail => detail.GuideId).HasColumnName("guide_id");
    }
}
