using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="ExposureContact"/> onto the <c>exposure_contacts</c> table, column
/// by column and under the legacy names.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is. The table is
/// <c>exposure_contacts</c>, the plural every table here is given.
/// <see cref="ExposureContact.ExposureId"/> is required where the legacy
/// <c>exposure_id</c> is <c>NOT NULL</c> and cascades on delete: a contact is a
/// detail of its exposure and goes when the exposure does, as a test goes with
/// its specimen. <see cref="ExposureContact.PersonId"/> is required where the
/// legacy <c>person_id</c> is <c>NOT NULL</c> and is a <see cref="DeleteBehavior"/>
/// of <c>Restrict</c>, as <see cref="Lab.PersonId"/> is: a person with a contact
/// record against them is not deleted out from under it. The contact type is
/// bounded at the code bound the other configurations use; the disposition is a
/// plain nullable int. <see cref="ExposureContact.PersonConditionId"/>, the
/// contact event, is an optional foreign key to the reports that does not
/// cascade: the exposure already cascades from a report, and SQL Server
/// refuses a second cascade path into the same table.
/// </remarks>
internal sealed class ExposureContactConfiguration : IEntityTypeConfiguration<ExposureContact>
{
    /// <summary>
    /// Room for a short coded value: the contact type code, held at the bound
    /// <see cref="PersonConditionConfiguration"/> gives every code.
    /// </summary>
    private const int CodeLength = 20;

    public void Configure(EntityTypeBuilder<ExposureContact> builder)
    {
        builder.ToTable("exposure_contacts");
        builder.HasKey(contact => contact.Id);

        builder.Property(contact => contact.Id).HasColumnName("id");
        builder.Property(contact => contact.ExposureId).HasColumnName("exposure_id");
        builder.Property(contact => contact.PersonId).HasColumnName("person_id");
        builder.Property(contact => contact.ExposureStart).HasColumnName("exposure_start");
        builder.Property(contact => contact.ExposureEnd).HasColumnName("exposure_end");
        builder.Property(contact => contact.DispositionId).HasColumnName("disposition_id");
        builder.Property(contact => contact.ContactType)
            .HasColumnName("contact_type")
            .HasMaxLength(CodeLength);
        builder.Property(contact => contact.DispositionDate).HasColumnName("disposition_date");
        builder.Property(contact => contact.PersonConditionId).HasColumnName("person_condition_id");

        // No navigation on either side. A contact is a detail of its exposure,
        // so it cascades: deleting the exposure deletes its contacts.
        builder.HasOne<Exposure>()
            .WithMany()
            .HasForeignKey(contact => contact.ExposureId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // No navigation on either side. Deleting the person is refused rather
        // than cascaded, as a specimen's person is, because the contact record
        // is a record of its own.
        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(contact => contact.PersonId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // No navigation on either side. The contact event is optional and is
        // not deleted with the contact row, nor the row with the event.
        builder.HasOne<PersonCondition>()
            .WithMany()
            .HasForeignKey(contact => contact.PersonConditionId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
