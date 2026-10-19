using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="PersonTelephone"/> onto the <c>person_telephones</c> table.
/// </summary>
/// <remarks>
/// Internal, and the number's four parts are bounded at the lengths a
/// telephone number has: the legacy columns are unbounded <c>VARCHAR</c>, and
/// nothing in that data comes near these. <see cref="PersonTelephone.Note"/>
/// is left unbounded, because it holds prose.
/// </remarks>
internal sealed class PersonTelephoneConfiguration : IEntityTypeConfiguration<PersonTelephone>
{
    public void Configure(EntityTypeBuilder<PersonTelephone> builder)
    {
        builder.ToTable("person_telephones");
        builder.HasKey(telephone => telephone.Id);

        builder.Property(telephone => telephone.Id).HasColumnName("id");
        builder.Property(telephone => telephone.PersonId).HasColumnName("person_id");
        builder.Property(telephone => telephone.TelephoneType)
            .HasColumnName("telephone_type")
            .HasMaxLength(20);
        builder.Property(telephone => telephone.CountryCode)
            .HasColumnName("country_code")
            .HasMaxLength(10);
        builder.Property(telephone => telephone.AreaCode)
            .HasColumnName("area_code")
            .HasMaxLength(10);
        builder.Property(telephone => telephone.PhoneNumber)
            .HasColumnName("phone_number")
            .HasMaxLength(30);
        builder.Property(telephone => telephone.Extension)
            .HasColumnName("extension")
            .HasMaxLength(10);
        builder.Property(telephone => telephone.Beginning).HasColumnName("beginning");
        builder.Property(telephone => telephone.Note).HasColumnName("note");

        builder.HasOne<Person>()
            .WithMany(person => person.Telephones)
            .HasForeignKey(telephone => telephone.PersonId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
