using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="CoreElementOverride"/> onto the <c>core_element_overrides</c>
/// table: one row per element, condition and agency, and never a row that
/// names neither a condition nor an agency, which would only be the element's
/// default under another name.
/// </summary>
/// <remarks>
/// The element, the condition and the agency are real foreign keys, because
/// all three are real tables; an override goes with its element, and a
/// condition or agency an override names cannot be removed under it.
/// </remarks>
internal sealed class CoreElementOverrideConfiguration : IEntityTypeConfiguration<CoreElementOverride>
{
    public void Configure(EntityTypeBuilder<CoreElementOverride> builder)
    {
        builder.ToTable("core_element_overrides", table => table.HasCheckConstraint(
            "ck_core_element_overrides_condition_or_agency",
            "condition_id IS NOT NULL OR agency_id IS NOT NULL"));
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.CoreElementId).HasColumnName("core_element_id");
        builder.Property(o => o.ConditionId).HasColumnName("condition_id");
        builder.Property(o => o.AgencyId).HasColumnName("agency_id");
        builder.Property(o => o.Hidden).HasColumnName("hidden");

        // Unfiltered: EF would otherwise filter out rows with a null condition
        // or agency, and those are exactly the condition-only and agency-only
        // overrides that must not be duplicated. SQL Server's unique index
        // treats nulls as equal, which is the one-row-per-combination wanted.
        builder.HasIndex(o => new { o.CoreElementId, o.ConditionId, o.AgencyId })
            .IsUnique()
            .HasFilter(null);

        builder.HasOne<CoreElement>()
            .WithMany()
            .HasForeignKey(o => o.CoreElementId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Condition>()
            .WithMany()
            .HasForeignKey(o => o.ConditionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Agency>()
            .WithMany()
            .HasForeignKey(o => o.AgencyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
