using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="Outbreak"/> onto the <c>outbreaks</c> table, column by column and
/// under the legacy <c>outbreak</c> names.
/// </summary>
/// <remarks>
/// Internal, as every configuration here is: nothing outside this module says
/// what the schema is, and <see cref="SurvsysDbContext"/> finds it by scanning
/// the assembly. The table is <c>outbreaks</c>, the plural every table here is
/// given over the legacy singular.
///
/// The condition and the owning agency are real foreign keys with no
/// navigation, deleted with <see cref="DeleteBehavior.Restrict"/> as
/// <see cref="PersonConditionConfiguration"/> refuses deleting a condition a
/// report is against: an outbreak is the record, not a detail that goes with
/// what it points at. <see cref="Outbreak.Name"/> is required and bounded as
/// <see cref="AgencyConfiguration"/> bounds an agency's name; the description
/// is free text and left unbounded as <see cref="NoteConfiguration"/> leaves a
/// note's; the number is bounded at a short handle's length.
/// </remarks>
internal sealed class OutbreakConfiguration : IEntityTypeConfiguration<Outbreak>
{
    private const int NameLength = 200;

    private const int NumberLength = 50;

    public void Configure(EntityTypeBuilder<Outbreak> builder)
    {
        builder.ToTable("outbreaks");
        builder.HasKey(outbreak => outbreak.Id);

        builder.Property(outbreak => outbreak.Id).HasColumnName("id");
        builder.Property(outbreak => outbreak.Number)
            .HasColumnName("number")
            .HasMaxLength(NumberLength);
        builder.Property(outbreak => outbreak.Name)
            .HasColumnName("name")
            .HasMaxLength(NameLength)
            .IsRequired();
        builder.Property(outbreak => outbreak.OutbreakDate).HasColumnName("outbreak_date");
        builder.Property(outbreak => outbreak.Description).HasColumnName("description");
        builder.Property(outbreak => outbreak.ConditionId).HasColumnName("condition_id");
        builder.Property(outbreak => outbreak.AgencyId).HasColumnName("agency_id");
        builder.Property(outbreak => outbreak.Status)
            .HasColumnName("status")
            .IsRequired();
        builder.Property(outbreak => outbreak.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasOne<Condition>()
            .WithMany()
            .HasForeignKey(outbreak => outbreak.ConditionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Agency>()
            .WithMany()
            .HasForeignKey(outbreak => outbreak.AgencyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
