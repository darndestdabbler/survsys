using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="FormSection"/> onto the <c>form_sections</c> table, keyed by id
/// and cascaded with the form it belongs to.
/// </summary>
/// <remarks>
/// Internal, as every configuration here is. The form cascades — a section is
/// a part of the form and goes when the form does — as
/// <see cref="OutbreakReportSectionConfiguration"/> cascades a report with its
/// outbreak. <see cref="FormSection.Title"/> is required and bounded as a
/// name is.
/// </remarks>
internal sealed class FormSectionConfiguration : IEntityTypeConfiguration<FormSection>
{
    private const int TitleLength = 200;

    public void Configure(EntityTypeBuilder<FormSection> builder)
    {
        builder.ToTable("form_sections");
        builder.HasKey(section => section.Id);

        builder.Property(section => section.Id).HasColumnName("id");
        builder.Property(section => section.FormId).HasColumnName("form_id");
        builder.Property(section => section.Title)
            .HasColumnName("title")
            .HasMaxLength(TitleLength)
            .IsRequired();
        builder.Property(section => section.SortOrder).HasColumnName("sort_order");

        builder.HasOne<Form>()
            .WithMany()
            .HasForeignKey(section => section.FormId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
