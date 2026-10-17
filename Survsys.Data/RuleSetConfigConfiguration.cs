using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="RuleSetConfig"/> onto the <c>rule_set_config</c> table, column by
/// column and under the legacy names.
/// </summary>
/// <remarks>
/// Internal, because nothing outside this module may say what the schema is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly.
/// </remarks>
internal sealed class RuleSetConfigConfiguration : IEntityTypeConfiguration<RuleSetConfig>
{
    public void Configure(EntityTypeBuilder<RuleSetConfig> builder)
    {
        builder.ToTable("rule_set_config");
        builder.HasKey(config => config.Id);

        builder.Property(config => config.Id).HasColumnName("id");
        builder.Property(config => config.FormatType)
            .HasColumnName("format_type")
            .HasMaxLength(50);
        builder.Property(config => config.ValueSetName)
            .HasColumnName("value_set_name")
            .HasMaxLength(200);
        builder.Property(config => config.Repeating).HasColumnName("repeating");
    }
}
