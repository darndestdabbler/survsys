using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="ConditionTreatment"/> onto the <c>condition_treatments</c> table,
/// under the legacy column names.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is. The table is
/// <c>condition_treatments</c>, the plural every table here is given. The key
/// is the pair of ids, because the legacy join has no identity column and the
/// link is the pair: one treatment is listed for one condition once.
///
/// Deleting is refused from the condition's side and cascaded from the
/// treatment's, as <see cref="PersonConditionLabConfiguration"/> refuses from
/// the report's side and cascades from the specimen's: a condition with
/// treatments listed for it cannot be deleted out from under them, and a
/// treatment that is removed from the catalogue takes its listings with it,
/// because a listing is a detail of the treatment and not a record of its own.
/// </remarks>
internal sealed class ConditionTreatmentConfiguration
    : IEntityTypeConfiguration<ConditionTreatment>
{
    public void Configure(EntityTypeBuilder<ConditionTreatment> builder)
    {
        builder.ToTable("condition_treatments");
        builder.HasKey(link => new { link.ConditionId, link.TreatmentId });

        builder.Property(link => link.ConditionId).HasColumnName("condition_id");
        builder.Property(link => link.TreatmentId).HasColumnName("treatment_id");

        builder.HasOne<Condition>()
            .WithMany()
            .HasForeignKey(link => link.ConditionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Treatment>()
            .WithMany()
            .HasForeignKey(link => link.TreatmentId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
