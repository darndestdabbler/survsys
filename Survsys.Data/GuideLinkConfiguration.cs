using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="GuideLink"/> onto the <c>guide_link</c> table, column by column
/// and under the legacy names.
/// </summary>
/// <remarks>
/// Internal, because nothing outside this module may say what the schema is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. Keyed by
/// the guide-and-linked-guide pair, so a guide links another once.
/// </remarks>
internal sealed class GuideLinkConfiguration : IEntityTypeConfiguration<GuideLink>
{
    public void Configure(EntityTypeBuilder<GuideLink> builder)
    {
        builder.ToTable("guide_link");
        builder.HasKey(link => new { link.GuideId, link.LinkedGuideId });

        builder.Property(link => link.GuideId).HasColumnName("guide_id");
        builder.Property(link => link.LinkedGuideId).HasColumnName("linked_guide_id");
    }
}
