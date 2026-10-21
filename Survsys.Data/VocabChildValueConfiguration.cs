using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="VocabChildValue"/> onto the <c>vocab_child_vocab</c> table,
/// column by column and under the legacy names where there is one.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is. One lab's code is
/// another lab's different concept, and one code can be both a specimen and a
/// result, so the unique index is over the lab, the category and the code
/// together — the key the matcher resolves a lab-specific override by.
/// </remarks>
internal sealed class VocabChildValueConfiguration : IEntityTypeConfiguration<VocabChildValue>
{
    public void Configure(EntityTypeBuilder<VocabChildValue> builder)
    {
        builder.ToTable("vocab_child_vocab");
        builder.HasKey(child => child.Id);

        builder.Property(child => child.Id).HasColumnName("id");
        builder.Property(child => child.LabId).HasColumnName("lab_id");
        builder.Property(child => child.Category)
            .HasColumnName("category")
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(child => child.ChildCode)
            .HasColumnName("child_code")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(child => child.MasterId).HasColumnName("master_id");

        builder.HasIndex(child => new { child.LabId, child.Category, child.ChildCode }).IsUnique();
    }
}
