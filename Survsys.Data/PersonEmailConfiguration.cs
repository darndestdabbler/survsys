using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="PersonEmail"/> onto the <c>person_emails</c> table.
/// </summary>
internal sealed class PersonEmailConfiguration : IEntityTypeConfiguration<PersonEmail>
{
    public void Configure(EntityTypeBuilder<PersonEmail> builder)
    {
        builder.ToTable("person_emails");
        builder.HasKey(email => email.Id);

        builder.Property(email => email.Id).HasColumnName("id");
        builder.Property(email => email.PersonId).HasColumnName("person_id");
        builder.Property(email => email.EmailAddress)
            .HasColumnName("email_address")
            .HasMaxLength(320);
        builder.Property(email => email.CurrentEmail).HasColumnName("current_email");

        builder.HasOne<Person>()
            .WithMany(person => person.Emails)
            .HasForeignKey(email => email.PersonId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
