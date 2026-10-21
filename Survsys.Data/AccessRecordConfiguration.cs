using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="AccessRecord"/> onto the <c>access_records</c> table, indexed by
/// report and time, which is how the audit is read.
/// </summary>
internal sealed class AccessRecordConfiguration : IEntityTypeConfiguration<AccessRecord>
{
    public void Configure(EntityTypeBuilder<AccessRecord> builder)
    {
        builder.ToTable("access_records");
        builder.HasKey(record => record.Id);

        builder.Property(record => record.Id).HasColumnName("id");
        builder.Property(record => record.UserId).HasColumnName("user_id");
        builder.Property(record => record.PersonConditionId).HasColumnName("person_condition_id");
        builder.Property(record => record.Action).HasColumnName("action").HasMaxLength(16).IsRequired();
        builder.Property(record => record.Detail).HasColumnName("detail").HasMaxLength(256);
        builder.Property(record => record.AccessedAt).HasColumnName("accessed_at");

        builder.HasIndex(record => new { record.PersonConditionId, record.AccessedAt });

        // The user is a real foreign key, and users are deactivated rather than
        // deleted. The report is a plain column with no key: a key would either
        // take the audit with a deleted report or refuse the delete, and an
        // audit should neither forget nor stand in the way.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(record => record.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
