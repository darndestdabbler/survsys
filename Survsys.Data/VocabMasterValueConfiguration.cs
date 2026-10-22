using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="VocabMasterValue"/> onto the <c>vocab_master_vocab</c> table,
/// column by column and under the legacy names where there is one.
/// </summary>
/// <remarks>
/// Internal, because nothing outside this module may say what the schema is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. A concept
/// is unique within its category, so the index is over the pair: a child code
/// resolves to one master, and two masters of one name would make the CSV
/// import's upsert ambiguous.
/// </remarks>
internal sealed class VocabMasterValueConfiguration : IEntityTypeConfiguration<VocabMasterValue>
{
    public void Configure(EntityTypeBuilder<VocabMasterValue> builder)
    {
        builder.ToTable("vocab_master_vocab");
        builder.HasKey(master => master.Id);

        builder.Property(master => master.Id).HasColumnName("id");
        builder.Property(master => master.Category)
            .HasColumnName("category")
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(master => master.Concept)
            .HasColumnName("concept")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(master => master.AppValue)
            .HasColumnName("app_value")
            .HasMaxLength(200);

        builder.HasIndex(master => new { master.Category, master.Concept }).IsUnique();
    }
}
