using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="Note"/> onto the <c>notes</c> table, column by column and under
/// the legacy names.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is: nothing outside this
/// module may say what the schema is, and <see cref="SurvsysDbContext"/> finds
/// it by scanning the assembly.
///
/// The table is <c>notes</c> rather than the legacy singular, the plural every
/// table here is given. Nothing here is a foreign key: the report, the
/// exposure, the facility and the user are each a plain nullable int, as
/// <see cref="Exposure"/>'s ids are, so a note carries no navigation and no
/// delete rule of its own. The text is free text and left unbounded, as
/// <see cref="PersonAddress.Note"/> is, because a cap on prose would be a
/// guess; the note type is bounded at the code bound the other configurations
/// use. <see cref="Note.CreatedAt"/> and <see cref="Note.Struckthrough"/> are
/// required: a note is written at a moment, and it is either struck through or
/// it is not.
/// </remarks>
internal sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    /// <summary>
    /// Room for a short coded value: the note type code, held at the bound
    /// <see cref="PersonConditionConfiguration"/> gives every code.
    /// </summary>
    private const int CodeLength = 20;

    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.ToTable("notes");
        builder.HasKey(note => note.Id);

        builder.Property(note => note.Id).HasColumnName("id");
        builder.Property(note => note.Text).HasColumnName("note");
        builder.Property(note => note.Struckthrough)
            .HasColumnName("struckthrough")
            .IsRequired();
        builder.Property(note => note.UserId).HasColumnName("user_id");
        builder.Property(note => note.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        builder.Property(note => note.ExposureId).HasColumnName("exposure_id");
        builder.Property(note => note.PersonConditionId).HasColumnName("person_condition_id");
        builder.Property(note => note.FacilityId).HasColumnName("facility_id");
        builder.Property(note => note.NoteType)
            .HasColumnName("note_type")
            .HasMaxLength(CodeLength);
    }
}
