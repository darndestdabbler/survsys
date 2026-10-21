using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="HepLabGroupTestType"/> onto the
/// <c>nmi_hep_lab_group_test_type</c> table, under the legacy NMI column names.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. Deleting
/// the group cascades to its test types.
/// </remarks>
internal sealed class HepLabGroupTestTypeConfiguration : IEntityTypeConfiguration<HepLabGroupTestType>
{
    public void Configure(EntityTypeBuilder<HepLabGroupTestType> builder)
    {
        builder.ToTable("nmi_hep_lab_group_test_type");
        builder.HasKey(item => new { item.HepLabGroupId, item.TestTypeId });

        builder.Property(item => item.HepLabGroupId).HasColumnName("hep_lab_group_id");
        builder.Property(item => item.TestTypeId).HasColumnName("lab_test_type");
        builder.Property(item => item.TestGroupName)
            .HasColumnName("test_group_name")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(item => item.SortOrder).HasColumnName("sort_order");

        builder.HasOne<HepLabGroup>()
            .WithMany()
            .HasForeignKey(item => item.HepLabGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
