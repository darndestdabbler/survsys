using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="AttachmentFile"/> onto the <c>attachment_files</c> table, column
/// by column and under the legacy names.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is: nothing outside this
/// module may say what the schema is, and <see cref="SurvsysDbContext"/> finds
/// it by scanning the assembly. The table is <c>attachment_files</c>, the
/// plural every table here is given.
///
/// Two columns and nothing else. <see cref="AttachmentFile.Data"/> is required
/// — a file row with no bytes is not a file — and left unbounded, which on SQL
/// Server is <c>varbinary(max)</c>: how large a file may be is the API tier's
/// to say and it says it on the way in, where a cap here would be a second
/// limit nobody reads until a row is refused.
///
/// Nothing here is a foreign key. The pointer between the pair is
/// <see cref="Attachment.AttachmentFileId"/>, declared in
/// <see cref="AttachmentConfiguration"/> on the side that holds it, so this
/// table knows nothing about what describes it.
/// </remarks>
internal sealed class AttachmentFileConfiguration : IEntityTypeConfiguration<AttachmentFile>
{
    public void Configure(EntityTypeBuilder<AttachmentFile> builder)
    {
        builder.ToTable("attachment_files");
        builder.HasKey(file => file.Id);

        builder.Property(file => file.Id).HasColumnName("id");
        builder.Property(file => file.Data)
            .HasColumnName("data")
            .IsRequired();
    }
}
