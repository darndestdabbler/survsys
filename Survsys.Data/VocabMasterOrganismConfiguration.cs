using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="VocabMasterOrganism"/> onto the <c>vocab_master_organism</c>
/// table, column by column and under the legacy names.
/// </summary>
/// <remarks>
/// Internal, because nothing outside this module may say what the schema is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. The
/// primary SNOMED code is the table's natural lookup key, so it carries a
/// unique index: the matcher resolves by it, and two organisms for one SNOMED
/// would make the resolution ambiguous. The alternate code is not indexed,
/// because it is a secondary alias rather than the key matching resolves by.
/// </remarks>
internal sealed class VocabMasterOrganismConfiguration : IEntityTypeConfiguration<VocabMasterOrganism>
{
    public void Configure(EntityTypeBuilder<VocabMasterOrganism> builder)
    {
        builder.ToTable("vocab_master_organism");
        builder.HasKey(organism => organism.Id);

        builder.Property(organism => organism.Id).HasColumnName("id");
        builder.Property(organism => organism.Snomed)
            .HasColumnName("snomed")
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(organism => organism.SnomedAlt)
            .HasColumnName("snomed_alt")
            .HasMaxLength(20);
        builder.Property(organism => organism.OrganismName)
            .HasColumnName("organism")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(organism => organism.ConditionId).HasColumnName("condition_id");

        builder.HasIndex(organism => organism.Snomed).IsUnique();
    }
}
