using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="PersonVaccine"/> onto the <c>person_vaccines</c> table, column by
/// column and under the legacy <c>person_vaccine</c> names.
/// </summary>
/// <remarks>
/// Internal, as every configuration here is. The table is
/// <c>person_vaccines</c>, the plural every table here is given over the legacy
/// singular. The case is a required foreign key that cascades, as
/// <see cref="PersonConditionLabConfiguration"/> cascades a link from the
/// specimen's side — a record is a detail of its case and goes with it; the
/// vaccine is a required foreign key into the catalogue that is refused on
/// delete, as a report refuses deleting the condition it is against. The
/// comment is free text and left unbounded as <see cref="NoteConfiguration"/>
/// leaves a note's; the lot number is bounded at a short handle's length.
/// </remarks>
internal sealed class PersonVaccineConfiguration : IEntityTypeConfiguration<PersonVaccine>
{
    private const int LotNumberLength = 100;

    public void Configure(EntityTypeBuilder<PersonVaccine> builder)
    {
        builder.ToTable("person_vaccines");
        builder.HasKey(record => record.Id);

        builder.Property(record => record.Id).HasColumnName("id");
        builder.Property(record => record.PersonConditionId).HasColumnName("person_condition_id");
        builder.Property(record => record.VaccineId).HasColumnName("vaccine_id");
        builder.Property(record => record.AdministeredDate).HasColumnName("administered_date");
        builder.Property(record => record.DoseNumber).HasColumnName("dose_number");
        builder.Property(record => record.LotNumber)
            .HasColumnName("lot_number")
            .HasMaxLength(LotNumberLength);
        builder.Property(record => record.ExpirationDate).HasColumnName("expiration_date");
        builder.Property(record => record.Comment).HasColumnName("comment");
        builder.Property(record => record.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasOne<PersonCondition>()
            .WithMany()
            .HasForeignKey(record => record.PersonConditionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Vaccine>()
            .WithMany()
            .HasForeignKey(record => record.VaccineId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
