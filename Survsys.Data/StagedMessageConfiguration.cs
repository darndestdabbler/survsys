using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="StagedMessage"/> onto the legacy <c>system_messages</c> table,
/// column by column and under the legacy names where there is one.
/// </summary>
/// <remarks>
/// Internal, because nothing outside this module may say what the schema is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly, as it
/// finds the others. The raw message and the exception detail are unbounded
/// text: a lab message is as long as it is, and an exception detail carries
/// whatever the parser said. The review status is a short code held against
/// <see cref="MessageReviewStatusRules"/>, the length a code needs and no more.
/// </remarks>
internal sealed class StagedMessageConfiguration : IEntityTypeConfiguration<StagedMessage>
{
    private const int CodeLength = 20;
    private const int SourceLength = 200;

    public void Configure(EntityTypeBuilder<StagedMessage> builder)
    {
        builder.ToTable("system_messages");
        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id).HasColumnName("id");
        builder.Property(message => message.RawMessage)
            .HasColumnName("message")
            .IsRequired();
        builder.Property(message => message.Source)
            .HasColumnName("source")
            .HasMaxLength(SourceLength);
        builder.Property(message => message.ReceivedAt).HasColumnName("received_at");
        builder.Property(message => message.ReviewStatus)
            .HasColumnName("final_status")
            .HasMaxLength(CodeLength)
            .IsRequired();
        builder.Property(message => message.QaFlagged).HasColumnName("qa_flag");
        builder.Property(message => message.ExceptionDetail).HasColumnName("exception_detail");
    }
}
