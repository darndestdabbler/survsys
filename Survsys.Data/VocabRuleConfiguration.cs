using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="VocabRule"/> onto the <c>vocab_rules</c> table, column by column.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is. Intake reads a
/// condition's rules of one kind together, so the index is over the condition
/// and the kind.
/// </remarks>
internal sealed class VocabRuleConfiguration : IEntityTypeConfiguration<VocabRule>
{
    public void Configure(EntityTypeBuilder<VocabRule> builder)
    {
        builder.ToTable("vocab_rules");
        builder.HasKey(rule => rule.Id);

        builder.Property(rule => rule.Id).HasColumnName("id");
        builder.Property(rule => rule.ConditionId).HasColumnName("condition_id");
        builder.Property(rule => rule.Kind)
            .HasColumnName("kind")
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(rule => rule.ExecutionOrder).HasColumnName("execution_order");
        builder.Property(rule => rule.AllowNewCase).HasColumnName("allow_new_cmr");
        builder.Property(rule => rule.AllowUpdateCase).HasColumnName("allow_update_cmr");
        builder.Property(rule => rule.IsSurveillance).HasColumnName("is_surveillance");

        builder.HasIndex(rule => new { rule.ConditionId, rule.Kind });
    }
}
