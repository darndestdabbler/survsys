using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="PersonTreatment"/> onto the <c>person_treatments</c> table, under
/// the legacy <c>person_treatment</c> names.
/// </summary>
/// <remarks>
/// Internal, as every configuration here is. The case cascades and the
/// treatment is refused on delete, as <see cref="PersonVaccineConfiguration"/>
/// does for the vaccine: a record is a detail of its case, and a catalogue
/// treatment with records against it cannot be deleted out from under them.
/// </remarks>
internal sealed class PersonTreatmentConfiguration : IEntityTypeConfiguration<PersonTreatment>
{
    private const int DoseLength = 100;

    private const int CommentLength = 1_000;

    public void Configure(EntityTypeBuilder<PersonTreatment> builder)
    {
        builder.ToTable("person_treatments");
        builder.HasKey(record => record.Id);

        builder.Property(record => record.Id).HasColumnName("id");
        builder.Property(record => record.PersonConditionId).HasColumnName("person_condition_id");
        builder.Property(record => record.TreatmentId).HasColumnName("treatment_id");
        builder.Property(record => record.TreatmentDate).HasColumnName("treatment_date");
        builder.Property(record => record.StopTreatmentDate).HasColumnName("stop_treatment_date");
        builder.Property(record => record.Dose)
            .HasColumnName("dose")
            .HasMaxLength(DoseLength);
        builder.Property(record => record.Comment)
            .HasColumnName("treatment_comment")
            .HasMaxLength(CommentLength);
        builder.Property(record => record.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasOne<PersonCondition>()
            .WithMany()
            .HasForeignKey(record => record.PersonConditionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Treatment>()
            .WithMany()
            .HasForeignKey(record => record.TreatmentId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
