using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="LabTest"/> onto the <c>lab_tests</c> table, column by column and
/// under the legacy names.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is. The table is
/// <c>lab_tests</c>, the plural every table here is given.
/// <see cref="LabTest.LabId"/> is required where the legacy <c>lab_id</c> is
/// <c>NOT NULL</c> and cascades on delete: a test is a detail of its specimen
/// and goes when the specimen does. The LOINC code and the reference range are
/// bounded over the legacy data rather than on it.
/// </remarks>
internal sealed class LabTestConfiguration : IEntityTypeConfiguration<LabTest>
{
    public void Configure(EntityTypeBuilder<LabTest> builder)
    {
        builder.ToTable("lab_tests");
        builder.HasKey(test => test.Id);

        builder.Property(test => test.Id).HasColumnName("id");
        builder.Property(test => test.LabId).HasColumnName("lab_id");
        builder.Property(test => test.TestTypeId).HasColumnName("test_type_id");
        builder.Property(test => test.TestStatusId).HasColumnName("test_status_id");
        builder.Property(test => test.LoincCode)
            .HasColumnName("loinc_code")
            .HasMaxLength(50);
        builder.Property(test => test.ReferenceRange)
            .HasColumnName("reference_range")
            .HasMaxLength(200);
        builder.Property(test => test.LabTestDate).HasColumnName("lab_test_date");

        // No navigation on either side. A test is a detail of its specimen, so
        // it cascades: deleting the specimen deletes its tests.
        builder.HasOne<Lab>()
            .WithMany()
            .HasForeignKey(test => test.LabId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
