using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="ConditionLabOverride"/> onto the <c>nmi_condition_lab_override</c>
/// table, under the legacy NMI column names.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. Unique on
/// the condition, kind and item, so an item is overridden once; deleting the
/// condition cascades to its overrides.
/// </remarks>
internal sealed class ConditionLabOverrideConfiguration : IEntityTypeConfiguration<ConditionLabOverride>
{
    public void Configure(EntityTypeBuilder<ConditionLabOverride> builder)
    {
        builder.ToTable("nmi_condition_lab_override");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.ConditionId).HasColumnName("condition_id");
        builder.Property(item => item.Kind)
            .HasColumnName("kind")
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(item => item.ItemId).HasColumnName("item_id");
        builder.Property(item => item.Include).HasColumnName("include");

        builder.HasIndex(item => new { item.ConditionId, item.Kind, item.ItemId }).IsUnique();

        builder.HasOne<Condition>()
            .WithMany()
            .HasForeignKey(item => item.ConditionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
