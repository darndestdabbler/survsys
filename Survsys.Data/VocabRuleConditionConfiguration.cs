using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="VocabRuleCondition"/> onto the <c>vocab_rule_conditions</c>
/// table, column by column.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is. A rule's conditions
/// are read together, so the rule id is indexed.
/// </remarks>
internal sealed class VocabRuleConditionConfiguration : IEntityTypeConfiguration<VocabRuleCondition>
{
    public void Configure(EntityTypeBuilder<VocabRuleCondition> builder)
    {
        builder.ToTable("vocab_rule_conditions");
        builder.HasKey(condition => condition.Id);

        builder.Property(condition => condition.Id).HasColumnName("id");
        builder.Property(condition => condition.VocabRuleId).HasColumnName("vocab_rule_id");
        builder.Property(condition => condition.Field)
            .HasColumnName("field")
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(condition => condition.Operator)
            .HasColumnName("operator")
            .HasMaxLength(5)
            .IsRequired();
        builder.Property(condition => condition.Value)
            .HasColumnName("value")
            .HasMaxLength(200);
        builder.Property(condition => condition.AndOr)
            .HasColumnName("and_or")
            .HasMaxLength(5);

        builder.HasIndex(condition => condition.VocabRuleId);
    }
}
