using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="VocabMasterLoinc"/> onto the <c>vocab_master_loinc</c> table,
/// column by column and under the legacy names.
/// </summary>
/// <remarks>
/// Internal, because nothing outside this module may say what the schema is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. The code
/// is the table's natural lookup key, so it carries a unique index: the
/// matcher resolves by it, and two master rows for one LOINC would make the
/// resolution ambiguous.
/// </remarks>
internal sealed class VocabMasterLoincConfiguration : IEntityTypeConfiguration<VocabMasterLoinc>
{
    public void Configure(EntityTypeBuilder<VocabMasterLoinc> builder)
    {
        builder.ToTable("vocab_master_loinc");
        builder.HasKey(loinc => loinc.Id);

        builder.Property(loinc => loinc.Id).HasColumnName("id");
        builder.Property(loinc => loinc.Loinc)
            .HasColumnName("loinc")
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(loinc => loinc.ConceptName)
            .HasColumnName("concept_name")
            .HasMaxLength(200);
        builder.Property(loinc => loinc.ConditionId).HasColumnName("condition_id");
        builder.Property(loinc => loinc.OrganismId).HasColumnName("organism_id");
        builder.Property(loinc => loinc.OrganismFromResult).HasColumnName("organism_from_result");

        builder.HasIndex(loinc => loinc.Loinc).IsUnique();
    }
}
