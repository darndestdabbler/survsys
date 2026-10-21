using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="RuleSetValueMap"/> onto the <c>rule_set_value_map</c> table,
/// column by column and under the legacy names.
/// </summary>
/// <remarks>
/// Internal, because nothing outside this module may say what the schema is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly.
/// </remarks>
internal sealed class RuleSetValueMapConfiguration : IEntityTypeConfiguration<RuleSetValueMap>
{
    public void Configure(EntityTypeBuilder<RuleSetValueMap> builder)
    {
        builder.ToTable("rule_set_value_map");
        builder.HasKey(map => map.Id);

        builder.Property(map => map.Id).HasColumnName("id");
        builder.Property(map => map.RuleSetConfigId).HasColumnName("rule_set_config_id");
        builder.Property(map => map.ValueFrom)
            .HasColumnName("value_from")
            .HasMaxLength(500);
        builder.Property(map => map.ValueTo)
            .HasColumnName("value_to")
            .HasMaxLength(500);
        builder.Property(map => map.OtherSpecified)
            .HasColumnName("other_specified")
            .HasMaxLength(500);
    }
}
