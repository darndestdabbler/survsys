using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="ExposurePlace"/> onto the <c>exposure_places</c> table, column by
/// column and under the legacy names.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is. The table is
/// <c>exposure_places</c>, the plural every table here is given. Nothing here
/// is a foreign key: the place type is a plain nullable int, as every coded id
/// in the Clinical and Laboratory slices is, and the exposure that points at
/// this place does so by a plain int of its own. The address columns are
/// bounded at the caps <see cref="PersonAddressConfiguration"/> gives a person's
/// address, the state at the code bound the other configurations use, and the
/// name and the phone over the legacy data rather than on it.
///
/// The coordinates are the one decision the legacy declaration does not make
/// for us. Its <c>NUMERIC(9)</c> is nine digits and no decimal places, which
/// holds a whole number of degrees and nothing a map could use; the precision
/// here keeps the nine digits and gives six of them to the fraction, which
/// places a point to about a tenth of a metre and leaves three for the degrees
/// a longitude needs.
/// </remarks>
internal sealed class ExposurePlaceConfiguration : IEntityTypeConfiguration<ExposurePlace>
{
    /// <summary>
    /// Room for a short coded value: the state code the place's address
    /// carries, held at the bound <see cref="PersonConditionConfiguration"/>
    /// and <see cref="PersonConfiguration"/> give every code.
    /// </summary>
    private const int CodeLength = 20;

    public void Configure(EntityTypeBuilder<ExposurePlace> builder)
    {
        builder.ToTable("exposure_places");
        builder.HasKey(place => place.Id);

        builder.Property(place => place.Id).HasColumnName("id");
        builder.Property(place => place.ExposurePlaceTypeId).HasColumnName("exposure_place_type_id");
        builder.Property(place => place.Name)
            .HasColumnName("name")
            .HasMaxLength(200);
        builder.Property(place => place.Street)
            .HasColumnName("street")
            .HasMaxLength(200);
        builder.Property(place => place.City)
            .HasColumnName("city")
            .HasMaxLength(100);
        builder.Property(place => place.Zip)
            .HasColumnName("zip")
            .HasMaxLength(20);
        builder.Property(place => place.State)
            .HasColumnName("state")
            .HasMaxLength(CodeLength);
        builder.Property(place => place.Latitude)
            .HasColumnName("latitude")
            .HasPrecision(9, 6);
        builder.Property(place => place.Longitude)
            .HasColumnName("longitude")
            .HasPrecision(9, 6);
        builder.Property(place => place.Phone)
            .HasColumnName("phone")
            .HasMaxLength(50);
    }
}
