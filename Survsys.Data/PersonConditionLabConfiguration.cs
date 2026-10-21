using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="PersonConditionLab"/> onto the <c>person_condition_labs</c>
/// table, under the legacy column names.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is. The table is
/// <c>person_condition_labs</c>, the plural every table here is given. The key
/// is the pair of ids, because the legacy join has no identity column and the
/// link is the pair: one specimen is linked to one report once.
///
/// Deleting is refused from the report's side and cascaded from the specimen's.
/// A report with specimens against it cannot be deleted out from under them,
/// as a report cannot be when it has a person or a condition; a specimen that
/// is deleted takes its link with it, because the link is a detail of the
/// specimen's attachment and not a record of its own.
/// </remarks>
internal sealed class PersonConditionLabConfiguration
    : IEntityTypeConfiguration<PersonConditionLab>
{
    public void Configure(EntityTypeBuilder<PersonConditionLab> builder)
    {
        builder.ToTable("person_condition_labs");
        builder.HasKey(link => new { link.PersonConditionId, link.LabId });

        builder.Property(link => link.PersonConditionId).HasColumnName("person_condition_id");
        builder.Property(link => link.LabId).HasColumnName("lab_id");

        builder.HasOne<PersonCondition>()
            .WithMany()
            .HasForeignKey(link => link.PersonConditionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Lab>()
            .WithMany()
            .HasForeignKey(link => link.LabId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
