using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

internal sealed class PersonNameHistoryConfiguration : IEntityTypeConfiguration<PersonNameHistory>
{
    /// <summary>The length <see cref="PersonConfiguration"/> gives a person's name columns.</summary>
    private const int NameLength = 200;

    public void Configure(EntityTypeBuilder<PersonNameHistory> builder)
    {
        builder.ToTable("person_name_history");
        builder.HasKey(name => name.Id);

        builder.Property(name => name.Id).HasColumnName("id");
        builder.Property(name => name.PersonId).HasColumnName("person_id");
        builder.Property(name => name.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(NameLength)
            .IsRequired();
        builder.Property(name => name.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(NameLength);
        builder.Property(name => name.MiddleName)
            .HasColumnName("middle_name")
            .HasMaxLength(NameLength);
        builder.Property(name => name.ChangedOn).HasColumnName("changed_on");

        builder.HasOne<Person>()
            .WithMany(person => person.NameHistory)
            .HasForeignKey(name => name.PersonId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
