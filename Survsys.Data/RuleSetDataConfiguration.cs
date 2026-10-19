using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="RuleSetData"/> onto the <c>rule_set_data</c> table, column by
/// column and under the legacy names.
/// </summary>
/// <remarks>
/// Internal, because nothing outside this module may say what the schema is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly.
/// </remarks>
internal sealed class RuleSetDataConfiguration : IEntityTypeConfiguration<RuleSetData>
{
    public void Configure(EntityTypeBuilder<RuleSetData> builder)
    {
        builder.ToTable("rule_set_data");
        builder.HasKey(data => data.Id);

        builder.Property(data => data.Id).HasColumnName("id");
        builder.Property(data => data.RuleSetConfigId).HasColumnName("rule_set_config_id");
        builder.Property(data => data.DataName)
            .HasColumnName("data_name")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(data => data.DataValue)
            .HasColumnName("data_value")
            .HasMaxLength(1000);
    }
}
