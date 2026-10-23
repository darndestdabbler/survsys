using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

internal sealed class PersonIdentifierConfiguration : IEntityTypeConfiguration<PersonIdentifier>
{
    public void Configure(EntityTypeBuilder<PersonIdentifier> builder)
    {
        builder.ToTable("person_identifiers");
        builder.HasKey(identifier => identifier.Id);

        builder.Property(identifier => identifier.Id).HasColumnName("id");
        builder.Property(identifier => identifier.PersonId).HasColumnName("person_id");
        builder.Property(identifier => identifier.IdentifierType)
            .HasColumnName("identifier_type")
            .HasMaxLength(20);
        builder.Property(identifier => identifier.Value)
            .HasColumnName("value")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(identifier => identifier.AssigningAuthority)
            .HasColumnName("assigning_authority")
            .HasMaxLength(100);
        builder.Property(identifier => identifier.AssigningAuthorityType)
            .HasColumnName("assigning_authority_type")
            .HasMaxLength(10);

        builder.HasIndex(identifier => identifier.Value);

        builder.HasOne<Person>()
            .WithMany(person => person.Identifiers)
            .HasForeignKey(identifier => identifier.PersonId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
