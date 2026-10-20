using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="CodeValue"/> onto the <c>code_values</c> table, seeded with
/// <see cref="CodeValueSeed"/>.
/// </summary>
/// <remarks>
/// Internal, as every configuration here is. A code is unique within its list,
/// which is the unique index on the pair; the same code may stand on two lists
/// (<c>UNK</c> is on several).
/// </remarks>
internal sealed class CodeValueConfiguration : IEntityTypeConfiguration<CodeValue>
{
    private const int ListLength = 50;

    private const int CodeLength = 100;

    private const int DescriptionLength = 400;

    public void Configure(EntityTypeBuilder<CodeValue> builder)
    {
        builder.ToTable("code_values");
        builder.HasKey(code => code.Id);

        builder.Property(code => code.Id).HasColumnName("id");
        builder.Property(code => code.List)
            .HasColumnName("list")
            .HasMaxLength(ListLength)
            .IsRequired();
        builder.Property(code => code.Code)
            .HasColumnName("code")
            .HasMaxLength(CodeLength)
            .IsRequired();
        builder.Property(code => code.Description)
            .HasColumnName("description")
            .HasMaxLength(DescriptionLength);
        builder.Property(code => code.SortOrder).HasColumnName("sort_order");
        builder.Property(code => code.Active).HasColumnName("active");
        builder.Property(code => code.State)
            .HasColumnName("state")
            .HasMaxLength(2);
        builder.Property(code => code.Fips)
            .HasColumnName("fips")
            .HasMaxLength(5);
        builder.Property(code => code.Kind)
            .HasColumnName("kind")
            .HasMaxLength(20);

        builder.HasIndex(code => new { code.List, code.Code }).IsUnique();

        builder.HasData(CodeValueSeed.Rows);
    }
}
