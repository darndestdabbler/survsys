using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="EcrConditionMapping"/> onto the <c>ecr_condition_mapping</c> table.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. Unique on
/// code system and code, which is how a code is found. A condition deleted
/// from the catalogue leaves its codes unmapped rather than taking them with
/// it.
/// </remarks>
internal sealed class EcrConditionMappingConfiguration : IEntityTypeConfiguration<EcrConditionMapping>
{
    public void Configure(EntityTypeBuilder<EcrConditionMapping> builder)
    {
        builder.ToTable("ecr_condition_mapping");
        builder.HasKey(mapping => mapping.Id);

        builder.Property(mapping => mapping.Id).HasColumnName("id");
        builder.Property(mapping => mapping.CodeSystem).HasColumnName("code_system").HasMaxLength(100).IsRequired();
        builder.Property(mapping => mapping.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(mapping => mapping.DisplayName).HasColumnName("display_name").HasMaxLength(500);
        builder.Property(mapping => mapping.ConditionId).HasColumnName("condition_id");

        builder.HasIndex(mapping => new { mapping.CodeSystem, mapping.Code }).IsUnique();

        builder.HasOne<Condition>()
            .WithMany()
            .HasForeignKey(mapping => mapping.ConditionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
