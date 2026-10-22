using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="RuleSetExpression"/> onto the <c>rule_set_expression</c> table,
/// column by column and under the legacy names.
/// </summary>
/// <remarks>
/// Internal, because nothing outside this module may say what the schema is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly.
/// </remarks>
internal sealed class RuleSetExpressionConfiguration : IEntityTypeConfiguration<RuleSetExpression>
{
    public void Configure(EntityTypeBuilder<RuleSetExpression> builder)
    {
        builder.ToTable("rule_set_expression");
        builder.HasKey(expression => expression.Id);

        builder.Property(expression => expression.Id).HasColumnName("id");
        builder.Property(expression => expression.RuleSetConfigId).HasColumnName("rule_set_config_id");
        builder.Property(expression => expression.Value)
            .HasColumnName("value")
            .HasMaxLength(500);
        builder.Property(expression => expression.OtherSpecified)
            .HasColumnName("other_specified")
            .HasMaxLength(500);
        builder.Property(expression => expression.ExecutionOrder).HasColumnName("execution_order");
    }
}
