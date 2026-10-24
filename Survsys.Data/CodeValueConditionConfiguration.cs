using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="CodeValueCondition"/> onto the <c>code_value_conditions</c>
/// table.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is. The key is the pair
/// of ids. Deleting cascades from both sides: the link is a detail of the
/// test type and of the condition alike, and neither is held back by it.
/// </remarks>
internal sealed class CodeValueConditionConfiguration
    : IEntityTypeConfiguration<CodeValueCondition>
{
    public void Configure(EntityTypeBuilder<CodeValueCondition> builder)
    {
        builder.ToTable("code_value_conditions");
        builder.HasKey(link => new { link.CodeValueId, link.ConditionId });

        builder.Property(link => link.CodeValueId).HasColumnName("code_value_id");
        builder.Property(link => link.ConditionId).HasColumnName("condition_id");

        builder.HasOne<CodeValue>()
            .WithMany()
            .HasForeignKey(link => link.CodeValueId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Condition>()
            .WithMany()
            .HasForeignKey(link => link.ConditionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
