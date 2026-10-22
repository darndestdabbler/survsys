using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="LabTestResult"/> onto the <c>lab_test_results</c> table, column
/// by column and under the legacy names.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is. The table is
/// <c>lab_test_results</c>, the plural every table here is given.
/// <see cref="LabTestResult.LabTestId"/> is required where the legacy
/// <c>lab_test_id</c> is <c>NOT NULL</c> and cascades on delete: a result is a
/// detail of its test and goes when the test does. The value, the coded result
/// and the units are bounded over the legacy data; the comment is left
/// unbounded, because it holds prose the way the condition lead-ins do and a
/// cap there would be a guess at it.
/// </remarks>
internal sealed class LabTestResultConfiguration : IEntityTypeConfiguration<LabTestResult>
{
    public void Configure(EntityTypeBuilder<LabTestResult> builder)
    {
        builder.ToTable("lab_test_results");
        builder.HasKey(result => result.Id);

        builder.Property(result => result.Id).HasColumnName("id");
        builder.Property(result => result.LabTestId).HasColumnName("lab_test_id");
        builder.Property(result => result.OrganismId).HasColumnName("organism_id");
        builder.Property(result => result.ResultValue)
            .HasColumnName("result_value")
            .HasMaxLength(2000);
        builder.Property(result => result.TestResult)
            .HasColumnName("test_result")
            .HasMaxLength(100);
        builder.Property(result => result.Comment).HasColumnName("comment");
        builder.Property(result => result.Units)
            .HasColumnName("units")
            .HasMaxLength(50);
        builder.Property(result => result.PrimaryLabResult).HasColumnName("primary_lab_result");

        // No navigation on either side. A result is a detail of its test, so
        // it cascades: deleting the test deletes its results.
        builder.HasOne<LabTest>()
            .WithMany()
            .HasForeignKey(result => result.LabTestId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
