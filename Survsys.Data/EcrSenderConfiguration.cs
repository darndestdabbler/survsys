using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="EcrSender"/> onto the <c>ecr_sender</c> table.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. Unique on
/// the sender id, which is how a document's sender is found; the id is as
/// long as a document id may be.
/// </remarks>
internal sealed class EcrSenderConfiguration : IEntityTypeConfiguration<EcrSender>
{
    public void Configure(EntityTypeBuilder<EcrSender> builder)
    {
        builder.ToTable("ecr_sender");
        builder.HasKey(sender => sender.Id);

        builder.Property(sender => sender.Id).HasColumnName("id");
        builder.Property(sender => sender.SenderId).HasColumnName("sender_id").HasMaxLength(300).IsRequired();
        builder.Property(sender => sender.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(sender => sender.Stage).HasColumnName("stage").HasMaxLength(20).IsRequired();
        builder.Property(sender => sender.TakesLabResults).HasColumnName("takes_lab_results");

        builder.HasIndex(sender => sender.SenderId).IsUnique();
    }
}
