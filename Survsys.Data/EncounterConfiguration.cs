using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="Encounter"/> onto the <c>encounters</c> table, under the legacy
/// <c>encounter</c> names.
/// </summary>
/// <remarks>
/// Internal, as every configuration here is. The case cascades, as
/// <see cref="PersonVaccineConfiguration"/>'s does; the facility is optional
/// and refused on delete, so a facility with encounters naming it stays.
/// </remarks>
internal sealed class EncounterConfiguration : IEntityTypeConfiguration<Encounter>
{
    private const int EncounterTypeLength = 50;

    private const int DescriptionLength = 1_000;

    public void Configure(EntityTypeBuilder<Encounter> builder)
    {
        builder.ToTable("encounters");
        builder.HasKey(encounter => encounter.Id);

        builder.Property(encounter => encounter.Id).HasColumnName("id");
        builder.Property(encounter => encounter.PersonConditionId)
            .HasColumnName("person_condition_id");
        builder.Property(encounter => encounter.EncounterDate)
            .HasColumnName("encounter_date")
            .IsRequired();
        builder.Property(encounter => encounter.EncounterType)
            .HasColumnName("encounter_type")
            .HasMaxLength(EncounterTypeLength);
        builder.Property(encounter => encounter.FacilityId).HasColumnName("facility_id");
        builder.Property(encounter => encounter.Description)
            .HasColumnName("description")
            .HasMaxLength(DescriptionLength);
        builder.Property(encounter => encounter.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasOne<PersonCondition>()
            .WithMany()
            .HasForeignKey(encounter => encounter.PersonConditionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Facility>()
            .WithMany()
            .HasForeignKey(encounter => encounter.FacilityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
