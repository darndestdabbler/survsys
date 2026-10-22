using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="HepLabGroupCondition"/> onto the
/// <c>nmi_hep_lab_group_condition</c> table.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. Keyed by
/// the condition; deleting the group or the condition cascades.
/// </remarks>
internal sealed class HepLabGroupConditionConfiguration : IEntityTypeConfiguration<HepLabGroupCondition>
{
    public void Configure(EntityTypeBuilder<HepLabGroupCondition> builder)
    {
        builder.ToTable("nmi_hep_lab_group_condition");
        builder.HasKey(member => member.ConditionId);

        builder.Property(member => member.HepLabGroupId).HasColumnName("hep_lab_group_id");
        builder.Property(member => member.ConditionId)
            .HasColumnName("condition_id")
            .ValueGeneratedNever();

        builder.HasOne<HepLabGroup>()
            .WithMany()
            .HasForeignKey(member => member.HepLabGroupId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Condition>()
            .WithMany()
            .HasForeignKey(member => member.ConditionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
