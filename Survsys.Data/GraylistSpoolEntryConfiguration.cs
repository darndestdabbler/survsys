using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="GraylistSpoolEntry"/> onto the legacy <c>graylist_spool</c> table,
/// column by column and under the legacy names.
/// </summary>
/// <remarks>
/// Internal, because nothing outside this module may say what the schema is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. The event
/// is a plain column with no key, as <see cref="AccessRecordConfiguration"/>
/// holds its report: the spool should neither forget a drained event nor stand
/// in the way of deleting one.
/// </remarks>
internal sealed class GraylistSpoolEntryConfiguration : IEntityTypeConfiguration<GraylistSpoolEntry>
{
    public void Configure(EntityTypeBuilder<GraylistSpoolEntry> builder)
    {
        builder.ToTable("graylist_spool");
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id).HasColumnName("id");
        builder.Property(entry => entry.CreatedAt).HasColumnName("created_at");
        builder.Property(entry => entry.Port)
            .HasColumnName("port")
            .HasMaxLength(100);
        builder.Property(entry => entry.Sent).HasColumnName("sent");
        builder.Property(entry => entry.PersonConditionId).HasColumnName("person_condition_id");
    }
}
