using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="FormAnswer"/> onto the <c>form_answers</c> table, keyed by id and
/// unique on the assignment-and-question pair.
/// </summary>
/// <remarks>
/// Internal, as every configuration here is. The assignment cascades — an
/// answer goes with the assignment it is of — while the question is refused
/// deletion with <see cref="DeleteBehavior.Restrict"/>, as the seeded
/// references on <see cref="OutbreakReportSectionConfiguration"/> are: a
/// question is a part of the form definition and is not deleted out from under
/// the answers that point at it. The unique index on the pair is what makes
/// saving an answer again a save over the one row. The text answer is free
/// text and left unbounded as <see cref="NoteConfiguration"/> leaves a note's;
/// the code is bounded at a short code's length as
/// <see cref="FormQuestionOptionConfiguration"/> bounds the option it names.
/// </remarks>
internal sealed class FormAnswerConfiguration : IEntityTypeConfiguration<FormAnswer>
{
    private const int CodeLength = 50;

    public void Configure(EntityTypeBuilder<FormAnswer> builder)
    {
        builder.ToTable("form_answers");
        builder.HasKey(answer => answer.Id);

        builder.Property(answer => answer.Id).HasColumnName("id");
        builder.Property(answer => answer.FormAssignmentId).HasColumnName("form_assignment_id");
        builder.Property(answer => answer.FormQuestionId).HasColumnName("form_question_id");
        builder.Property(answer => answer.TextAnswer).HasColumnName("text_answer");
        builder.Property(answer => answer.Code)
            .HasColumnName("code")
            .HasMaxLength(CodeLength);
        builder.Property(answer => answer.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(answer => new { answer.FormAssignmentId, answer.FormQuestionId })
            .IsUnique();

        builder.HasOne<FormAssignment>()
            .WithMany()
            .HasForeignKey(answer => answer.FormAssignmentId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<FormQuestion>()
            .WithMany()
            .HasForeignKey(answer => answer.FormQuestionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
