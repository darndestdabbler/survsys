using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="FormQuestionOption"/> onto the <c>form_question_options</c> table,
/// keyed by id and cascaded with the question it belongs to.
/// </summary>
/// <remarks>
/// Internal, as every configuration here is. The question cascades — a choice
/// is a part of its question and goes when the question does — as
/// <see cref="FormQuestionConfiguration"/> cascades a question with its
/// section. <see cref="FormQuestionOption.Code"/> is bounded at a short code's
/// length and <see cref="FormQuestionOption.Label"/> at a name's, both required.
/// </remarks>
internal sealed class FormQuestionOptionConfiguration
    : IEntityTypeConfiguration<FormQuestionOption>
{
    private const int CodeLength = 50;

    private const int LabelLength = 200;

    public void Configure(EntityTypeBuilder<FormQuestionOption> builder)
    {
        builder.ToTable("form_question_options");
        builder.HasKey(option => option.Id);

        builder.Property(option => option.Id).HasColumnName("id");
        builder.Property(option => option.FormQuestionId).HasColumnName("form_question_id");
        builder.Property(option => option.Code)
            .HasColumnName("code")
            .HasMaxLength(CodeLength)
            .IsRequired();
        builder.Property(option => option.Label)
            .HasColumnName("label")
            .HasMaxLength(LabelLength)
            .IsRequired();
        builder.Property(option => option.SortOrder).HasColumnName("sort_order");

        builder.HasOne<FormQuestion>()
            .WithMany()
            .HasForeignKey(option => option.FormQuestionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
