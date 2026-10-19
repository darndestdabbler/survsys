using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="CaseTask"/> onto the <c>tasks</c> table, column by column and
/// under the legacy names.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is. The table is
/// <c>tasks</c> rather than the legacy singular, the plural every table here
/// is given — and the reason the entity could be named away from
/// <see cref="System.Threading.Tasks.Task"/> without the schema drifting from
/// the legacy one.
///
/// Nothing here is a foreign key: the report, the user, the category and the
/// first task of the series are each a plain nullable int, as
/// <see cref="NoteConfiguration"/>'s and <see cref="ExposureConfiguration"/>'s
/// are, so a task carries no navigation and no delete rule of its own. The
/// name and the notes are free text and left unbounded, as
/// <see cref="Note.Text"/> is; the priority, the status and the repeating
/// interval are bounded at the code bound the other configurations use.
/// <see cref="CaseTask.CreatedAt"/> is required, and
/// <see cref="CaseTask.UpdatedAt"/> is not: a task nothing has touched yet has
/// no such moment.
/// </remarks>
internal sealed class CaseTaskConfiguration : IEntityTypeConfiguration<CaseTask>
{
    /// <summary>
    /// Room for a short coded value: the priority, status and repeating
    /// interval codes, held at the bound
    /// <see cref="PersonConditionConfiguration"/> gives every code.
    /// </summary>
    private const int CodeLength = 20;

    public void Configure(EntityTypeBuilder<CaseTask> builder)
    {
        builder.ToTable("tasks");
        builder.HasKey(task => task.Id);

        builder.Property(task => task.Id).HasColumnName("id");
        builder.Property(task => task.Name).HasColumnName("name");
        builder.Property(task => task.Notes).HasColumnName("notes");
        builder.Property(task => task.Priority)
            .HasColumnName("priority")
            .HasMaxLength(CodeLength);
        builder.Property(task => task.PersonConditionId).HasColumnName("person_condition_id");
        builder.Property(task => task.UserId).HasColumnName("user_id");
        builder.Property(task => task.DueDate).HasColumnName("due_date");
        builder.Property(task => task.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        builder.Property(task => task.UpdatedAt).HasColumnName("updated_at");
        builder.Property(task => task.CategoryId).HasColumnName("category_id");
        builder.Property(task => task.Status)
            .HasColumnName("status")
            .HasMaxLength(CodeLength);
        builder.Property(task => task.RepeatingTaskId).HasColumnName("repeating_task_id");
        builder.Property(task => task.UntilDate).HasColumnName("until_date");
        builder.Property(task => task.RepeatingInterval)
            .HasColumnName("repeating_interval")
            .HasMaxLength(CodeLength);
    }
}
