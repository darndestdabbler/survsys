using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="Person"/> onto the <c>persons</c> table, column by column and
/// under the legacy names.
/// </summary>
/// <remarks>
/// Internal, because nothing outside this module may say what the schema is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly.
///
/// The table is <c>persons</c> rather than the legacy singular <c>person</c>,
/// which is the plural session 5 gave the <c>conditions</c> table; the columns
/// keep their legacy names, which is what a later data migration reads across.
///
/// Every string column here is bounded, where the legacy columns are
/// unbounded <c>VARCHAR</c>. Two reasons, and the first is the load-bearing
/// one: an unbounded string arrives on SQL Server as <c>nvarchar(max)</c>,
/// which cannot carry an index, and the name columns are what a list of people
/// is ordered by and what session 14's search will index. The second is that
/// the demographic values are reference codes — <c>F</c>, <c>H</c>, <c>es</c>,
/// <c>MEX</c> — and a column that holds a code does not need room for prose.
/// The bounds leave room over what the legacy data holds rather than sitting
/// on it.
/// </remarks>
internal sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    /// <summary>
    /// Room for a name. Long enough for any of the three name parts in the
    /// legacy data and for the hyphenated and multi-part names it does not
    /// happen to hold.
    /// </summary>
    private const int NameLength = 200;

    /// <summary>
    /// Room for a reference code — a gender, an ethnicity, a language or a
    /// country. The longest in the legacy data is three characters.
    /// </summary>
    private const int CodeLength = 20;

    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("persons");
        builder.HasKey(person => person.Id);

        builder.Property(person => person.Id).HasColumnName("id");
        builder.Property(person => person.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(NameLength)
            .IsRequired();
        builder.Property(person => person.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(NameLength);
        builder.Property(person => person.MiddleName)
            .HasColumnName("middle_name")
            .HasMaxLength(NameLength);
        builder.Property(person => person.BirthDate).HasColumnName("birth_date");
        builder.Property(person => person.DaysOldNoBirthday).HasColumnName("days_old_no_birthday");
        builder.Property(person => person.BirthGender)
            .HasColumnName("birth_gender")
            .HasMaxLength(CodeLength);
        builder.Property(person => person.CurrentGender)
            .HasColumnName("current_gender")
            .HasMaxLength(CodeLength);
        builder.Property(person => person.Ethnicity)
            .HasColumnName("ethnicity")
            .HasMaxLength(CodeLength);
        builder.Property(person => person.Language)
            .HasColumnName("language")
            .HasMaxLength(CodeLength);
        builder.Property(person => person.CountryOfBirth)
            .HasColumnName("country_of_birth")
            .HasMaxLength(CodeLength);
    }
}
