using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="Exposure"/> onto the <c>exposures</c> table, column by column and
/// under the legacy names.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is: nothing outside this
/// module may say what the schema is, and <see cref="SurvsysDbContext"/> finds
/// it by scanning the assembly.
///
/// The table is <c>exposures</c> rather than the legacy singular, the plural
/// every table here is given. Nothing here is a foreign key: the report, the
/// condition, the agency and the place are each a plain nullable int, as
/// <see cref="Lab"/>'s report and reference ids are, so an exposure carries no
/// navigation and no delete rule of its own — its contacts and sources carry
/// theirs against it. The name and the description are free text and left
/// unbounded, as <see cref="PersonAddress.Note"/> is, because a cap on prose
/// would be a guess.
/// </remarks>
internal sealed class ExposureConfiguration : IEntityTypeConfiguration<Exposure>
{
    public void Configure(EntityTypeBuilder<Exposure> builder)
    {
        builder.ToTable("exposures");
        builder.HasKey(exposure => exposure.Id);

        builder.Property(exposure => exposure.Id).HasColumnName("id");
        builder.Property(exposure => exposure.PersonConditionId).HasColumnName("person_condition_id");
        builder.Property(exposure => exposure.ConditionId).HasColumnName("condition_id");
        builder.Property(exposure => exposure.AgencyId).HasColumnName("agency_id");
        builder.Property(exposure => exposure.ExposurePlaceId).HasColumnName("exposure_place_id");
        builder.Property(exposure => exposure.ExposureStart).HasColumnName("exposure_start");
        builder.Property(exposure => exposure.ExposureEnd).HasColumnName("exposure_end");
        builder.Property(exposure => exposure.Name).HasColumnName("name");
        builder.Property(exposure => exposure.Description).HasColumnName("description");
    }
}
