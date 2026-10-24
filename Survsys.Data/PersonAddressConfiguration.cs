using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="PersonAddress"/> onto the <c>person_addresses</c> table.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is: nothing outside this
/// module may say what the schema is.
///
/// The address belongs to its person, so the foreign key is required and
/// deleting the person deletes the address with it. <see cref="Note"/> is left
/// unbounded, because it holds whatever an investigator wrote and a cap there
/// would be a guess at prose; the rest are bounded, and an unbounded string on
/// SQL Server is an <c>nvarchar(max)</c> that cannot be indexed.
/// </remarks>
internal sealed class PersonAddressConfiguration : IEntityTypeConfiguration<PersonAddress>
{
    public void Configure(EntityTypeBuilder<PersonAddress> builder)
    {
        builder.ToTable("person_addresses");
        builder.HasKey(address => address.Id);

        builder.Property(address => address.Id).HasColumnName("id");
        builder.Property(address => address.PersonId).HasColumnName("person_id");
        builder.Property(address => address.Street)
            .HasColumnName("street")
            .HasMaxLength(200);
        builder.Property(address => address.UnitNumber)
            .HasColumnName("unit_number")
            .HasMaxLength(50);
        builder.Property(address => address.City)
            .HasColumnName("city")
            .HasMaxLength(100);
        builder.Property(address => address.State)
            .HasColumnName("state")
            .HasMaxLength(20);
        builder.Property(address => address.PostalCode)
            .HasColumnName("postal_code")
            .HasMaxLength(20);
        builder.Property(address => address.County)
            .HasColumnName("county")
            .HasMaxLength(50);
        builder.Property(address => address.CountyEquivalent)
            .HasColumnName("county_equivalent")
            .HasMaxLength(50);
        builder.Property(address => address.Beginning).HasColumnName("beginning");
        builder.Property(address => address.Ending).HasColumnName("ending");
        builder.Property(address => address.Note).HasColumnName("note");
        builder.Property(address => address.ResidenceTypeId).HasColumnName("residence_type_id");
        builder.Property(address => address.Latitude)
            .HasColumnName("latitude")
            .HasPrecision(9, 6);
        builder.Property(address => address.Longitude)
            .HasColumnName("longitude")
            .HasPrecision(9, 6);
        builder.Property(address => address.GeocodedBy)
            .HasColumnName("geocoded_by")
            .HasMaxLength(10);

        // One current address a person: the one with no ending. Both indexes
        // are named, so the unique filtered one does not stand in for the
        // foreign key's own, which reads the historic rows too.
        builder.HasIndex(address => address.PersonId, "IX_person_addresses_person_id");
        builder.HasIndex(address => address.PersonId, "IX_person_addresses_person_id_current")
            .IsUnique()
            .HasFilter("[ending] IS NULL");

        builder.HasOne<Person>()
            .WithMany(person => person.Addresses)
            .HasForeignKey(address => address.PersonId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
