using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="RuleSetField"/> onto the <c>rule_set_field</c> table, column by
/// column and under the legacy names.
/// </summary>
/// <remarks>
/// Internal, because nothing outside this module may say what the schema is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly.
/// </remarks>
internal sealed class RuleSetFieldConfiguration : IEntityTypeConfiguration<RuleSetField>
{
    public void Configure(EntityTypeBuilder<RuleSetField> builder)
    {
        builder.ToTable("rule_set_field");
        builder.HasKey(field => field.Id);

        builder.Property(field => field.Id).HasColumnName("id");
        builder.Property(field => field.RuleSetConfigId).HasColumnName("rule_set_config_id");
        builder.Property(field => field.Field)
            .HasColumnName("field")
            .HasMaxLength(200)
            .IsRequired();
    }
}
