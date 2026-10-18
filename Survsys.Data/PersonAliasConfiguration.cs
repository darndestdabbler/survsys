using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="PersonAlias"/> onto the <c>person_aliases</c> table, keyed on
/// the person and the alias together as the legacy table is.
/// </summary>
/// <remarks>
/// The alias is bounded at the length a whole name needs, rather than at the
/// length of one name part: an alias is written as one string — "Maria Elena
/// Rodriguez-Garcia" — and not as three columns.
/// </remarks>
internal sealed class PersonAliasConfiguration : IEntityTypeConfiguration<PersonAlias>
{
    public void Configure(EntityTypeBuilder<PersonAlias> builder)
    {
        builder.ToTable("person_aliases");
        builder.HasKey(alias => new { alias.PersonId, alias.Alias });

        builder.Property(alias => alias.PersonId).HasColumnName("person_id");
        builder.Property(alias => alias.Alias)
            .HasColumnName("person_alias")
            .HasMaxLength(400)
            .IsRequired();

        builder.HasOne<Person>()
            .WithMany(person => person.Aliases)
            .HasForeignKey(alias => alias.PersonId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
