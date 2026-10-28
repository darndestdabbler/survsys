using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="EcrCaseValue"/> onto the <c>ecr_case_value</c> table.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. A value
/// belongs to one link and is deleted with it; it is indexed on the row it
/// names, which is how what an eICR wrote on a row is found. The written text
/// is unbounded, because a row is written as all of its fields.
/// </remarks>
internal sealed class EcrCaseValueConfiguration : IEntityTypeConfiguration<EcrCaseValue>
{
    public void Configure(EntityTypeBuilder<EcrCaseValue> builder)
    {
        builder.ToTable("ecr_case_value");
        builder.HasKey(value => value.Id);

        builder.Property(value => value.Id).HasColumnName("id");
        builder.Property(value => value.EcrCaseLinkId).HasColumnName("ecr_case_link_id");
        builder.Property(value => value.Target).HasColumnName("target").HasMaxLength(100).IsRequired();
        builder.Property(value => value.TargetId).HasColumnName("target_id");
        builder.Property(value => value.Key).HasColumnName("key").HasMaxLength(500).IsRequired();
        builder.Property(value => value.Written).HasColumnName("written");

        builder.HasOne(value => value.EcrCaseLink)
            .WithMany(link => link.Values)
            .HasForeignKey(value => value.EcrCaseLinkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(value => new { value.Target, value.TargetId });
    }
}
