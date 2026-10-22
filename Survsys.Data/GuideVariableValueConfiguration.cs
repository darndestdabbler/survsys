using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="GuideVariableValue"/> onto the <c>nmi_guide_variable</c> table, under
/// the legacy NMI column names.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. Deleting
/// the guide or the default cascades to the guide's value.
/// </remarks>
internal sealed class GuideVariableValueConfiguration : IEntityTypeConfiguration<GuideVariableValue>
{
    public void Configure(EntityTypeBuilder<GuideVariableValue> builder)
    {
        builder.ToTable("nmi_guide_variable");
        builder.HasKey(variable => new { variable.GuideId, variable.GuideVariableDefaultId });

        builder.Property(variable => variable.GuideId).HasColumnName("guide_id");
        builder.Property(variable => variable.GuideVariableDefaultId).HasColumnName("guide_variable_default_id");
        builder.Property(variable => variable.Value)
            .HasColumnName("value")
            .HasMaxLength(500);

        builder.HasOne<NotificationGuide>()
            .WithMany()
            .HasForeignKey(variable => variable.GuideId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<GuideVariableDefault>()
            .WithMany()
            .HasForeignKey(variable => variable.GuideVariableDefaultId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
