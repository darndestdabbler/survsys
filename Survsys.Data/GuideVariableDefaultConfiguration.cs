using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="GuideVariableDefault"/> onto the <c>nmi_guide_variable_default</c>
/// table, under the legacy NMI column names.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. Unique on
/// the name, so a variable has one default.
/// </remarks>
internal sealed class GuideVariableDefaultConfiguration : IEntityTypeConfiguration<GuideVariableDefault>
{
    public void Configure(EntityTypeBuilder<GuideVariableDefault> builder)
    {
        builder.ToTable("nmi_guide_variable_default");
        builder.HasKey(variable => variable.Id);

        builder.Property(variable => variable.Id).HasColumnName("id");
        builder.Property(variable => variable.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(variable => variable.DefaultValue)
            .HasColumnName("default_value")
            .HasMaxLength(500);

        builder.HasIndex(variable => variable.Name).IsUnique();
    }
}
