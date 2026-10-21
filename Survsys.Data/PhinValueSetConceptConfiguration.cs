using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="PhinValueSetConcept"/> onto the <c>nmi_phin_value_set</c> table,
/// under the legacy NMI column names.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. Unique on
/// the value set and concept code, the key a CSV import matches by.
/// </remarks>
internal sealed class PhinValueSetConceptConfiguration : IEntityTypeConfiguration<PhinValueSetConcept>
{
    public void Configure(EntityTypeBuilder<PhinValueSetConcept> builder)
    {
        builder.ToTable("nmi_phin_value_set");
        builder.HasKey(concept => concept.Id);

        builder.Property(concept => concept.Id).HasColumnName("id");
        builder.Property(concept => concept.ValueSetName)
            .HasColumnName("value_set_name")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(concept => concept.ConceptCode)
            .HasColumnName("concept_code")
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(concept => concept.PreferredConceptName)
            .HasColumnName("preferred_concept_name")
            .HasMaxLength(500)
            .IsRequired();
        builder.Property(concept => concept.Hl7Code)
            .HasColumnName("hl7_code")
            .HasMaxLength(50);
        builder.Property(concept => concept.CodeSystemOid)
            .HasColumnName("code_system_oid")
            .HasMaxLength(100);
        builder.Property(concept => concept.CdcName)
            .HasColumnName("cdc_name")
            .HasMaxLength(500);

        builder.HasIndex(concept => new { concept.ValueSetName, concept.ConceptCode }).IsUnique();
    }
}
