using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="EcrDocument"/> onto the <c>ecr_document</c> table.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. The
/// document id is unique, which is what takes a document once; the set id and
/// the referenced eICR id are indexed, because versions are found by the one
/// and an RR's eICR by the other. The raw XML is unbounded text, kept as it
/// arrived.
/// </remarks>
internal sealed class EcrDocumentConfiguration : IEntityTypeConfiguration<EcrDocument>
{
    private const int IdLength = 300;

    public void Configure(EntityTypeBuilder<EcrDocument> builder)
    {
        builder.ToTable("ecr_document");
        builder.HasKey(document => document.Id);

        builder.Property(document => document.Id).HasColumnName("id");
        builder.Property(document => document.DocumentId)
            .HasColumnName("document_id")
            .HasMaxLength(IdLength)
            .IsRequired();
        builder.Property(document => document.Kind).HasColumnName("kind").HasMaxLength(10).IsRequired();
        builder.Property(document => document.Feed).HasColumnName("feed").HasMaxLength(20).IsRequired();
        builder.Property(document => document.SetId).HasColumnName("set_id").HasMaxLength(IdLength);
        builder.Property(document => document.VersionNumber).HasColumnName("version_number");
        builder.Property(document => document.EicrDocumentId)
            .HasColumnName("eicr_document_id")
            .HasMaxLength(IdLength);
        builder.Property(document => document.RawXml).HasColumnName("raw_xml").IsRequired();
        builder.Property(document => document.Source).HasColumnName("source").HasMaxLength(260);
        builder.Property(document => document.ReceivedAt).HasColumnName("received_at");

        builder.HasIndex(document => document.DocumentId).IsUnique();
        builder.HasIndex(document => document.SetId);
        builder.HasIndex(document => document.EicrDocumentId);
    }
}
