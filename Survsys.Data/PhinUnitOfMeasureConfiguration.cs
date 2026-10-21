using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="PhinUnitOfMeasure"/> onto the <c>nmi_phin_unit_of_measure</c>
/// table, under the legacy NMI column names.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly. Unique on
/// the value set and concept code, the key a CSV import matches by.
/// </remarks>
internal sealed class PhinUnitOfMeasureConfiguration : IEntityTypeConfiguration<PhinUnitOfMeasure>
{
    public void Configure(EntityTypeBuilder<PhinUnitOfMeasure> builder)
    {
        builder.ToTable("nmi_phin_unit_of_measure");
        builder.HasKey(unit => unit.Id);

        builder.Property(unit => unit.Id).HasColumnName("id");
        builder.Property(unit => unit.ValueSetName)
            .HasColumnName("value_set_name")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(unit => unit.ConceptCode)
            .HasColumnName("concept_code")
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(unit => unit.PreferredConceptName)
            .HasColumnName("preferred_concept_name")
            .HasMaxLength(500)
            .IsRequired();
        builder.Property(unit => unit.Hl7Code)
            .HasColumnName("hl7_code")
            .HasMaxLength(50);
        builder.Property(unit => unit.CodeSystemOid)
            .HasColumnName("code_system_oid")
            .HasMaxLength(100);

        builder.HasIndex(unit => new { unit.ValueSetName, unit.ConceptCode }).IsUnique();
    }
}
