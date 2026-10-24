using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="PersonRace"/> onto the <c>person_races</c> table, keyed on the
/// person and the race together as the legacy table is.
/// </summary>
internal sealed class PersonRaceConfiguration : IEntityTypeConfiguration<PersonRace>
{
    public void Configure(EntityTypeBuilder<PersonRace> builder)
    {
        builder.ToTable("person_races");
        builder.HasKey(race => new { race.PersonId, race.Race });

        builder.Property(race => race.PersonId).HasColumnName("person_id");
        builder.Property(race => race.Race)
            .HasColumnName("race")
            .HasMaxLength(20)
            .IsRequired();

        builder.HasOne<Person>()
            .WithMany(person => person.Races)
            .HasForeignKey(race => race.PersonId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
