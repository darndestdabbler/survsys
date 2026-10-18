using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="FormQuestion"/> onto the <c>form_questions</c> table, keyed by id
/// and cascaded with the section it belongs to.
/// </summary>
/// <remarks>
/// Internal, as every configuration here is. The section cascades — a question
/// is a part of its section and goes when the section does — as
/// <see cref="FormSectionConfiguration"/> cascades a section with its form.
/// <see cref="FormQuestion.Text"/> is required and left unbounded as a
/// prompt's text can run long, as <see cref="NoteConfiguration"/> leaves a
/// note's; the help text is free text and left unbounded likewise.
/// </remarks>
internal sealed class FormQuestionConfiguration : IEntityTypeConfiguration<FormQuestion>
{
    public void Configure(EntityTypeBuilder<FormQuestion> builder)
    {
        builder.ToTable("form_questions");
        builder.HasKey(question => question.Id);

        builder.Property(question => question.Id).HasColumnName("id");
        builder.Property(question => question.FormSectionId).HasColumnName("form_section_id");
        builder.Property(question => question.Text)
            .HasColumnName("text")
            .IsRequired();
        builder.Property(question => question.HelpText).HasColumnName("help_text");
        builder.Property(question => question.AnswerType)
            .HasColumnName("answer_type")
            .IsRequired();
        builder.Property(question => question.IsRequired)
            .HasColumnName("is_required")
            .IsRequired();
        builder.Property(question => question.SortOrder).HasColumnName("sort_order");

        builder.HasOne<FormSection>()
            .WithMany()
            .HasForeignKey(question => question.FormSectionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
