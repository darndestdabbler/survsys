using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="Attachment"/> onto the <c>attachments</c> table, column by
/// column and under the legacy names.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is: nothing outside this
/// module may say what the schema is, and <see cref="SurvsysDbContext"/> finds
/// it by scanning the assembly. The table is <c>attachments</c>, the plural
/// every table here is given.
///
/// Two real foreign keys, both required and both cascading, declared with no
/// navigation on either side as <see cref="LabTestConfiguration"/>'s and
/// <see cref="ExposureContactConfiguration"/>'s are:
/// <see cref="Attachment.PersonConditionId"/> to <c>person_conditions</c>,
/// because an attachment is a detail of the report it is filed against and
/// goes when the report does, as a test goes with its specimen; and
/// <see cref="Attachment.AttachmentFileId"/> to <c>attachment_files</c>, so an
/// attachment cannot outlive the bytes it describes. The legacy leaves both
/// columns nullable and this schema does not, for the reason
/// <see cref="Attachment"/> gives.
///
/// <see cref="Attachment.Filename"/>, <see cref="Attachment.ContentType"/> and
/// <see cref="Attachment.Category"/> are required and bounded rather than left
/// as the legacy's unbounded nullable <c>VARCHAR</c>s: these are a file's
/// name, its media type and a code from a fixed list, each of which has a
/// length a reader can reason about, where <see cref="Note.Text"/> is left
/// unbounded because it holds whatever somebody wrote.
/// </remarks>
internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    /// <summary>
    /// Room for a file's name: the longest single path element a Windows
    /// client can hand over, so a name this tier stores is one the client it
    /// came from could hold.
    /// </summary>
    private const int FilenameLength = 260;

    /// <summary>
    /// Room for a media type. Long enough for the longest type anything
    /// attaches here — the Office XML document types run past seventy
    /// characters — without being room for a header somebody pasted in.
    /// </summary>
    private const int ContentTypeLength = 100;

    /// <summary>
    /// Room for a short coded value: the category code, held at the bound
    /// <see cref="PersonConditionConfiguration"/> gives every code.
    /// </summary>
    private const int CodeLength = 20;

    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("attachments");
        builder.HasKey(attachment => attachment.Id);

        builder.Property(attachment => attachment.Id).HasColumnName("id");
        builder.Property(attachment => attachment.Filename)
            .HasColumnName("filename")
            .HasMaxLength(FilenameLength)
            .IsRequired();
        builder.Property(attachment => attachment.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(ContentTypeLength)
            .IsRequired();
        builder.Property(attachment => attachment.Size)
            .HasColumnName("size")
            .IsRequired();
        builder.Property(attachment => attachment.Category)
            .HasColumnName("category")
            .HasMaxLength(CodeLength)
            .IsRequired();
        builder.Property(attachment => attachment.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        builder.Property(attachment => attachment.PersonConditionId)
            .HasColumnName("person_condition_id");
        builder.Property(attachment => attachment.AttachmentFileId)
            .HasColumnName("attachment_file_id");

        // No navigation on either side. An attachment is a detail of the
        // report it is filed against, so it cascades: deleting the report
        // deletes its attachments.
        builder.HasOne<PersonCondition>()
            .WithMany()
            .HasForeignKey(attachment => attachment.PersonConditionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // No navigation on either side. The bytes exist for this row alone, so
        // deleting the file row deletes the attachment that describes it --
        // there is no metadata here for a file that is gone.
        builder.HasOne<AttachmentFile>()
            .WithMany()
            .HasForeignKey(attachment => attachment.AttachmentFileId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
