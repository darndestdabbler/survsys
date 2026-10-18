using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="Form"/> onto the <c>forms</c> table, column by column and under
/// the legacy <c>form</c> names.
/// </summary>
/// <remarks>
/// Internal, as every configuration here is: nothing outside this module says
/// what the schema is, and <see cref="SurvsysDbContext"/> finds it by scanning
/// the assembly. The table is <c>forms</c>, the plural every table here is
/// given over the legacy singular.
///
/// The condition is a real foreign key with no navigation, refused deletion
/// with <see cref="DeleteBehavior.Restrict"/> as the condition on
/// <see cref="OutbreakConfiguration"/> is: a form is the record, not a detail
/// that goes with what it points at, and it is required because a form is
/// condition-specific. <see cref="Form.Name"/> is required and bounded as
/// <see cref="OutbreakConfiguration"/> bounds an outbreak's name; the
/// description is free text and left unbounded as
/// <see cref="NoteConfiguration"/> leaves a note's.
/// </remarks>
internal sealed class FormConfiguration : IEntityTypeConfiguration<Form>
{
    private const int NameLength = 200;

    public void Configure(EntityTypeBuilder<Form> builder)
    {
        builder.ToTable("forms");
        builder.HasKey(form => form.Id);

        builder.Property(form => form.Id).HasColumnName("id");
        builder.Property(form => form.Name)
            .HasColumnName("name")
            .HasMaxLength(NameLength)
            .IsRequired();
        builder.Property(form => form.Description).HasColumnName("description");
        builder.Property(form => form.ConditionId).HasColumnName("condition_id");
        builder.Property(form => form.AutoAssign)
            .HasColumnName("auto_assign")
            .IsRequired();
        builder.Property(form => form.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasOne<Condition>()
            .WithMany()
            .HasForeignKey(form => form.ConditionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
