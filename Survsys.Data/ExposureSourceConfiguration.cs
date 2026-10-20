using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="ExposureSource"/> onto the <c>exposure_sources</c> table, column
/// by column and under the legacy names.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is. The table is
/// <c>exposure_sources</c>, the plural every table here is given.
/// <see cref="ExposureSource.ExposureId"/> is required where the legacy
/// <c>exposure_id</c> is <c>NOT NULL</c> and cascades on delete: a source is a
/// detail of its exposure and goes when the exposure does. The person and the
/// report are plain nullable ints with no foreign key, because a source need
/// not be on record at all. The source type is bounded at the code bound the
/// other configurations use and the source name over the legacy data rather
/// than on it.
/// </remarks>
internal sealed class ExposureSourceConfiguration : IEntityTypeConfiguration<ExposureSource>
{
    /// <summary>
    /// Room for a short coded value: the source type code, held at the bound
    /// <see cref="PersonConditionConfiguration"/> gives every code.
    /// </summary>
    private const int CodeLength = 20;

    public void Configure(EntityTypeBuilder<ExposureSource> builder)
    {
        builder.ToTable("exposure_sources");
        builder.HasKey(source => source.Id);

        builder.Property(source => source.Id).HasColumnName("id");
        builder.Property(source => source.ExposureId).HasColumnName("exposure_id");
        builder.Property(source => source.PersonId).HasColumnName("person_id");
        builder.Property(source => source.PersonConditionId).HasColumnName("person_condition_id");
        builder.Property(source => source.SourceName)
            .HasColumnName("source_name")
            .HasMaxLength(200);
        builder.Property(source => source.SourceType)
            .HasColumnName("source_type")
            .HasMaxLength(CodeLength);

        // No navigation on either side. A source is a detail of its exposure,
        // so it cascades: deleting the exposure deletes its sources.
        builder.HasOne<Exposure>()
            .WithMany()
            .HasForeignKey(source => source.ExposureId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
