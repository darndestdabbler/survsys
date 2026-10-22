using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="OutbreakReportSection"/> onto the <c>outbreak_report_sections</c>
/// table, keyed by id and unique on the outbreak-and-section pair.
/// </summary>
/// <remarks>
/// Internal, as every configuration here is. The outbreak cascades — a report
/// goes with the outbreak it is of — while the report section and the author
/// are refused deletion with <see cref="DeleteBehavior.Restrict"/>: a seeded
/// section and a user are records of their own, and a user is deactivated
/// rather than deleted as <see cref="AccessRecordConfiguration"/> keeps its
/// author. The unique index on the pair is what makes writing a section again
/// a save over the one row. The report text is free text and left unbounded as
/// <see cref="NoteConfiguration"/> leaves a note's.
/// </remarks>
internal sealed class OutbreakReportSectionConfiguration
    : IEntityTypeConfiguration<OutbreakReportSection>
{
    public void Configure(EntityTypeBuilder<OutbreakReportSection> builder)
    {
        builder.ToTable("outbreak_report_sections");
        builder.HasKey(section => section.Id);

        builder.Property(section => section.Id).HasColumnName("id");
        builder.Property(section => section.OutbreakId).HasColumnName("outbreak_id");
        builder.Property(section => section.ReportSectionId).HasColumnName("report_section_id");
        builder.Property(section => section.UserId).HasColumnName("user_id");
        builder.Property(section => section.ReportText).HasColumnName("report_text");

        builder.HasIndex(section => new { section.OutbreakId, section.ReportSectionId }).IsUnique();

        builder.HasOne<Outbreak>()
            .WithMany()
            .HasForeignKey(section => section.OutbreakId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ReportSection>()
            .WithMany()
            .HasForeignKey(section => section.ReportSectionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(section => section.UserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
