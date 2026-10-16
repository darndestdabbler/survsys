using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="Lab"/> onto the <c>labs</c> table, column by column and under the
/// legacy names.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is: nothing outside this
/// module may say what the schema is, and <see cref="SurvsysDbContext"/> finds
/// it by scanning the assembly.
///
/// The table is <c>labs</c> rather than the legacy singular, the plural every
/// table here is given. <see cref="Lab.PersonId"/> is required where the legacy
/// <c>person_id</c> is <c>NOT NULL</c> and is a <see cref="DeleteBehavior"/> of
/// <c>Restrict</c>, as a case morbidity report's person is: a specimen is a
/// record of its own, and a person with lab work against them is not deleted
/// out from under it. The shortish string columns are bounded for the reason
/// the other configurations bound codes, and a cap over the legacy data rather
/// than on it; the accession number is the one a person is shown.
/// </remarks>
internal sealed class LabConfiguration : IEntityTypeConfiguration<Lab>
{
    public void Configure(EntityTypeBuilder<Lab> builder)
    {
        builder.ToTable("labs");
        builder.HasKey(lab => lab.Id);

        builder.Property(lab => lab.Id).HasColumnName("id");
        builder.Property(lab => lab.PersonId).HasColumnName("person_id");
        builder.Property(lab => lab.LabFacilityId).HasColumnName("lab_facility_id");
        builder.Property(lab => lab.SpecimenSourceId).HasColumnName("specimen_source_id");
        builder.Property(lab => lab.CollectionDate).HasColumnName("collection_date");
        builder.Property(lab => lab.AccessionNo)
            .HasColumnName("accession_no")
            .HasMaxLength(100);
        builder.Property(lab => lab.SpecimenSentToState).HasColumnName("specimen_sent_to_state");

        // No navigation on either side: nothing walks from a specimen to its
        // person or from a person to their specimens. Deleting is refused
        // rather than cascaded, as a report's person is, because a specimen is
        // the record itself.
        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(lab => lab.PersonId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
