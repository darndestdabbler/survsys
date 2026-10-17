using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="SavedView"/> onto the <c>saved_views</c> table, one name per
/// user. The views go with the user.
/// </summary>
internal sealed class SavedViewConfiguration : IEntityTypeConfiguration<SavedView>
{
    public void Configure(EntityTypeBuilder<SavedView> builder)
    {
        builder.ToTable("saved_views");
        builder.HasKey(view => view.Id);

        builder.Property(view => view.Id).HasColumnName("id");
        builder.Property(view => view.UserId).HasColumnName("user_id");
        builder.Property(view => view.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(view => view.Columns)
            .HasColumnName("columns")
            .HasMaxLength(1000)
            .IsRequired();
        builder.Property(view => view.ConditionId).HasColumnName("condition_id");
        builder.Property(view => view.AgencyId).HasColumnName("agency_id");
        builder.Property(view => view.WorkflowState).HasColumnName("workflow_state").HasMaxLength(50);
        builder.Property(view => view.CaseStatus).HasColumnName("case_status").HasMaxLength(50);
        builder.Property(view => view.OnsetFrom).HasColumnName("onset_from");
        builder.Property(view => view.OnsetTo).HasColumnName("onset_to");

        builder.HasIndex(view => new { view.UserId, view.Name }).IsUnique();

        // The condition and agency filters are plain columns with no key: a key
        // would either refuse a delete of the condition or agency or quietly
        // widen the filter, and a filter naming a record that is gone should
        // simply select nothing.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(view => view.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
