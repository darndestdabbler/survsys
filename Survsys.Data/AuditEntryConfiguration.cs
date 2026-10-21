using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="AuditEntry"/> onto the <c>audit_entries</c> table, indexed by
/// table and time, which is how the audit is read.
/// </summary>
/// <remarks>
/// Internal, as every configuration here is: nothing outside this module says
/// what the schema is, and <see cref="SurvsysDbContext"/> finds it by scanning
/// the assembly. The table is <c>audit_entries</c>, the plural every table here
/// is given over the legacy <c>audit</c> singular the triggers wrote. The
/// string columns are required and bounded as the other configurations bound
/// theirs; a rendered key fits a composite key as well as a single one.
/// </remarks>
internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    private const int TableNameLength = 128;
    private const int KeyValuesLength = 256;
    private const int OperationLength = 16;

    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entries");
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id).HasColumnName("id");
        builder.Property(entry => entry.TableName)
            .HasColumnName("table_name")
            .HasMaxLength(TableNameLength)
            .IsRequired();
        builder.Property(entry => entry.KeyValues)
            .HasColumnName("key_values")
            .HasMaxLength(KeyValuesLength)
            .IsRequired();
        builder.Property(entry => entry.Operation)
            .HasColumnName("operation")
            .HasMaxLength(OperationLength)
            .IsRequired();
        builder.Property(entry => entry.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        builder.Property(entry => entry.UserId).HasColumnName("user_id");
        builder.Property(entry => entry.OldValues).HasColumnName("old_values");
        builder.Property(entry => entry.NewValues).HasColumnName("new_values");

        builder.HasIndex(entry => new { entry.TableName, entry.OccurredAt });
    }
}
