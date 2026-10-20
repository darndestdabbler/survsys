using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="FormAssignment"/> onto the <c>form_assignments</c> table, keyed by
/// id and unique on the form-and-case pair.
/// </summary>
/// <remarks>
/// Internal, as every configuration here is. The case cascades — an assignment
/// goes with the case it is on — while the form is refused deletion with
/// <see cref="DeleteBehavior.Restrict"/>, as the seeded references on
/// <see cref="OutbreakReportSectionConfiguration"/> are: a form is a record of
/// its own and is not deleted out from under the cases that hold it. The unique
/// index on the pair is what makes assigning the same form again the same row
/// rather than another, so auto-assignment stays idempotent.
/// </remarks>
internal sealed class FormAssignmentConfiguration : IEntityTypeConfiguration<FormAssignment>
{
    public void Configure(EntityTypeBuilder<FormAssignment> builder)
    {
        builder.ToTable("form_assignments");
        builder.HasKey(assignment => assignment.Id);

        builder.Property(assignment => assignment.Id).HasColumnName("id");
        builder.Property(assignment => assignment.FormId).HasColumnName("form_id");
        builder.Property(assignment => assignment.PersonConditionId)
            .HasColumnName("person_condition_id");
        builder.Property(assignment => assignment.AssignedAt)
            .HasColumnName("assigned_at")
            .IsRequired();

        builder.HasIndex(assignment => new { assignment.FormId, assignment.PersonConditionId })
            .IsUnique();

        builder.HasOne<Form>()
            .WithMany()
            .HasForeignKey(assignment => assignment.FormId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PersonCondition>()
            .WithMany()
            .HasForeignKey(assignment => assignment.PersonConditionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
