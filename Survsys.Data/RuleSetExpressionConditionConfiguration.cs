using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="RuleSetExpressionCondition"/> onto the
/// <c>rule_set_expression_condition</c> table, column by column and under the
/// legacy names.
/// </summary>
/// <remarks>
/// Internal, because nothing outside this module may say what the schema is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly.
/// </remarks>
internal sealed class RuleSetExpressionConditionConfiguration
    : IEntityTypeConfiguration<RuleSetExpressionCondition>
{
    public void Configure(EntityTypeBuilder<RuleSetExpressionCondition> builder)
    {
        builder.ToTable("rule_set_expression_condition");
        builder.HasKey(condition => condition.Id);

        builder.Property(condition => condition.Id).HasColumnName("id");
        builder.Property(condition => condition.RuleSetExpressionId)
            .HasColumnName("rule_set_expression_id");
        builder.Property(condition => condition.Field)
            .HasColumnName("field")
            .HasMaxLength(200);
        builder.Property(condition => condition.Operator)
            .HasColumnName("operator")
            .HasMaxLength(10);
        builder.Property(condition => condition.Value)
            .HasColumnName("value")
            .HasMaxLength(500);
        builder.Property(condition => condition.AndOr)
            .HasColumnName("and_or")
            .HasMaxLength(10);
        builder.Property(condition => condition.ValueIsField).HasColumnName("value_is_field");
    }
}
