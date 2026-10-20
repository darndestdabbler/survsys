using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="PhinVariable"/> onto the <c>phin_var</c> table, column by column
/// and under the legacy names.
/// </summary>
/// <remarks>
/// Internal, because nothing outside this module may say what the schema is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. The
/// variable's identifier is its own key — a guide and a message refer to a
/// variable by it — so it is the primary key rather than a surrogate id.
/// </remarks>
internal sealed class PhinVariableConfiguration : IEntityTypeConfiguration<PhinVariable>
{
    public void Configure(EntityTypeBuilder<PhinVariable> builder)
    {
        builder.ToTable("phin_var");
        builder.HasKey(variable => variable.PhinVar);

        builder.Property(variable => variable.PhinVar)
            .HasColumnName("phin_var")
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(variable => variable.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .IsRequired();
        builder.Property(variable => variable.Identifier)
            .HasColumnName("identifier")
            .HasMaxLength(50);
        builder.Property(variable => variable.Priority)
            .HasColumnName("priority")
            .HasMaxLength(10);
        builder.Property(variable => variable.Hl7DataType)
            .HasColumnName("hl7_data_type")
            .HasMaxLength(20);
        builder.Property(variable => variable.RuleSetId).HasColumnName("rule_set_id");
        builder.Property(variable => variable.RuleSetConfigId).HasColumnName("rule_set_config_id");
        builder.Property(variable => variable.Status)
            .HasColumnName("status")
            .HasMaxLength(20);
        builder.Property(variable => variable.Note)
            .HasColumnName("note")
            .HasMaxLength(1000);
        builder.Property(variable => variable.CodeSystem)
            .HasColumnName("code_system")
            .HasMaxLength(50);
    }
}
