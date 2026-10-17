using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="ReportSection"/> onto the <c>report_sections</c> table, seeded
/// with the seven sections the legacy <c>report_section</c> table is seeded
/// with, in their order.
/// </summary>
/// <remarks>
/// Internal, as every configuration here is. The seed keeps each section's
/// order as the legacy <c>V0003__init.sql</c> inserts them, so section 1 is
/// Context/Background and section 7 is the Key Investigators heading; an
/// outbreak's <see cref="OutbreakReportSection"/> rows point at these ids.
/// </remarks>
internal sealed class ReportSectionConfiguration : IEntityTypeConfiguration<ReportSection>
{
    private const int DescriptionLength = 200;

    public void Configure(EntityTypeBuilder<ReportSection> builder)
    {
        builder.ToTable("report_sections");
        builder.HasKey(section => section.Id);

        builder.Property(section => section.Id).HasColumnName("id");
        builder.Property(section => section.Description)
            .HasColumnName("description")
            .HasMaxLength(DescriptionLength)
            .IsRequired();

        builder.HasData(
            new ReportSection { Id = 1, Description = "Context/Background" },
            new ReportSection { Id = 2, Description = "Initiation of Investigation" },
            new ReportSection { Id = 3, Description = "Investigation Methods" },
            new ReportSection { Id = 4, Description = "Investigation Findings/Results" },
            new ReportSection { Id = 5, Description = "Discussion and/or Conclusions" },
            new ReportSection
            {
                Id = 6,
                Description = "Recommendations for Controlling Disease and/or "
                    + "Preventing/Mitigating Exposure",
            },
            new ReportSection { Id = 7, Description = "Key Investigators and/or Report Authors" });
    }
}
