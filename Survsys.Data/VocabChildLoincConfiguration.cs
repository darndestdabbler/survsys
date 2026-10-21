using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="VocabChildLoinc"/> onto the <c>vocab_child_loinc</c> table,
/// column by column and under the legacy names.
/// </summary>
/// <remarks>
/// Internal, because nothing outside this module may say what the schema is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. A child
/// mapping is keyed by the lab and the local code together — one lab's code is
/// another lab's different test — so the unique index is over the pair, which
/// is the key the matcher resolves a lab-specific override by.
/// </remarks>
internal sealed class VocabChildLoincConfiguration : IEntityTypeConfiguration<VocabChildLoinc>
{
    public void Configure(EntityTypeBuilder<VocabChildLoinc> builder)
    {
        builder.ToTable("vocab_child_loinc");
        builder.HasKey(child => child.Id);

        builder.Property(child => child.Id).HasColumnName("id");
        builder.Property(child => child.ChildLoinc)
            .HasColumnName("child_loinc")
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(child => child.MasterLoincId).HasColumnName("master_loinc_id");
        builder.Property(child => child.LabId).HasColumnName("lab_id");
        builder.Property(child => child.ConceptName)
            .HasColumnName("concept_name")
            .HasMaxLength(200);
        builder.Property(child => child.Archived).HasColumnName("archived");

        builder.HasIndex(child => new { child.LabId, child.ChildLoinc }).IsUnique();
    }
}
