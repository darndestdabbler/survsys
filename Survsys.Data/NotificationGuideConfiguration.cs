using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="NotificationGuide"/> onto the <c>guide</c> table, column by
/// column and under the legacy names.
/// </summary>
/// <remarks>
/// Internal, because nothing outside this module may say what the schema is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly.
/// </remarks>
internal sealed class NotificationGuideConfiguration : IEntityTypeConfiguration<NotificationGuide>
{
    public void Configure(EntityTypeBuilder<NotificationGuide> builder)
    {
        builder.ToTable("guide");
        builder.HasKey(guide => guide.Id);

        builder.Property(guide => guide.Id).HasColumnName("id");
        builder.Property(guide => guide.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();
    }
}
