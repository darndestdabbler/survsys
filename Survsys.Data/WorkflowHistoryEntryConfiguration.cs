using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="WorkflowHistoryEntry"/> onto the <c>workflow_history</c> table,
/// indexed by report, which is how the history is read.
/// </summary>
internal sealed class WorkflowHistoryEntryConfiguration : IEntityTypeConfiguration<WorkflowHistoryEntry>
{
    /// <summary>
    /// Room for a transition or state code. The longest value is the
    /// nineteen-character <c>under_investigation</c> and the
    /// nineteen-character <c>assign_investigator</c>; the bound leaves room
    /// over them rather than sitting on them.
    /// </summary>
    private const int CodeLength = 32;

    public void Configure(EntityTypeBuilder<WorkflowHistoryEntry> builder)
    {
        builder.ToTable("workflow_history");
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id).HasColumnName("id");
        builder.Property(entry => entry.PersonConditionId).HasColumnName("person_condition_id");
        builder.Property(entry => entry.UserId).HasColumnName("user_id");
        builder.Property(entry => entry.OccurredAt).HasColumnName("occurred_at");
        builder.Property(entry => entry.Transition)
            .HasColumnName("transition")
            .HasMaxLength(CodeLength)
            .IsRequired();
        builder.Property(entry => entry.FromState)
            .HasColumnName("from_state")
            .HasMaxLength(CodeLength)
            .IsRequired();
        builder.Property(entry => entry.ToState)
            .HasColumnName("to_state")
            .HasMaxLength(CodeLength)
            .IsRequired();
        builder.Property(entry => entry.Note).HasColumnName("note");

        builder.HasIndex(entry => entry.PersonConditionId);

        // The history is a detail of its report and goes when the report does;
        // the user is a real foreign key that does not cascade, because users
        // are deactivated rather than deleted and a history that forgot who
        // acted would not be one.
        builder.HasOne<PersonCondition>()
            .WithMany()
            .HasForeignKey(entry => entry.PersonConditionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entry => entry.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
