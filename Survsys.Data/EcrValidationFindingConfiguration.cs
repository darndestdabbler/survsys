using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="EcrValidationFinding"/> onto the <c>ecr_validation_finding</c> table.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. A finding
/// belongs to one document and is deleted with it. The message and location
/// are unbounded, because a schematron's message and an XPath to a deeply
/// nested entry both run long.
/// </remarks>
internal sealed class EcrValidationFindingConfiguration : IEntityTypeConfiguration<EcrValidationFinding>
{
    public void Configure(EntityTypeBuilder<EcrValidationFinding> builder)
    {
        builder.ToTable("ecr_validation_finding");
        builder.HasKey(finding => finding.Id);

        builder.Property(finding => finding.Id).HasColumnName("id");
        builder.Property(finding => finding.EcrDocumentId).HasColumnName("ecr_document_id");
        builder.Property(finding => finding.Source).HasColumnName("source").HasMaxLength(20).IsRequired();
        builder.Property(finding => finding.Severity).HasColumnName("severity").HasMaxLength(10).IsRequired();
        builder.Property(finding => finding.Message).HasColumnName("message").IsRequired();
        builder.Property(finding => finding.Location).HasColumnName("location");

        builder.HasOne(finding => finding.EcrDocument)
            .WithMany(document => document.Findings)
            .HasForeignKey(finding => finding.EcrDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
