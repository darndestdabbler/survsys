using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="Vaccine"/> onto the <c>vaccines</c> table, under the legacy
/// <c>vaccine</c> names, seeded with the CDC vaccine codes.
/// </summary>
/// <remarks>
/// Internal, as every configuration here is. The table is <c>vaccines</c>, the
/// plural every table here is given over the legacy singular.
/// <see cref="Vaccine.Code"/> is required and carries a unique index where the
/// legacy column is <c>text NOT NULL UNIQUE</c>.
///
/// The seed is the CDC vaccine rows
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0169__create_vaccine_tables.sql</c>
/// inserts, the way <see cref="AgencyConfiguration"/> seeds the agencies and
/// <see cref="CoreElementConfiguration"/> seeds the core elements. Each row's
/// <see cref="Vaccine.Id"/> is its <see cref="Vaccine.SortOrder"/>, a stable
/// key for a fixed reference list, so a record's <c>vaccine_id</c> names the
/// same vaccine across environments.
/// </remarks>
internal sealed class VaccineConfiguration : IEntityTypeConfiguration<Vaccine>
{
    private const int CodeLength = 20;

    private const int DescriptionLength = 200;

    public void Configure(EntityTypeBuilder<Vaccine> builder)
    {
        builder.ToTable("vaccines");
        builder.HasKey(vaccine => vaccine.Id);

        builder.Property(vaccine => vaccine.Id).HasColumnName("id");
        builder.Property(vaccine => vaccine.Code)
            .HasColumnName("code")
            .HasMaxLength(CodeLength)
            .IsRequired();
        builder.Property(vaccine => vaccine.Description)
            .HasColumnName("description")
            .HasMaxLength(DescriptionLength);
        builder.Property(vaccine => vaccine.SortOrder).HasColumnName("sort_order");
        builder.Property(vaccine => vaccine.Active).HasColumnName("active");

        builder.HasIndex(vaccine => vaccine.Code).IsUnique();

        builder.HasData(
            new Vaccine { Id = 1, Code = "20", Description = "DTaP", SortOrder = 1 },
            new Vaccine { Id = 2, Code = "106", Description = "DTaP, 5 pertussis antigens", SortOrder = 2 },
            new Vaccine { Id = 3, Code = "107", Description = "DTaP, unspecified formulation", SortOrder = 3 },
            new Vaccine { Id = 4, Code = "146", Description = "DTaP-IPV-Hib-Hep B", SortOrder = 4 },
            new Vaccine { Id = 5, Code = "110", Description = "DTaP-Hep B-IPV", SortOrder = 5 },
            new Vaccine { Id = 6, Code = "50", Description = "DTaP-Hib", SortOrder = 6 },
            new Vaccine { Id = 7, Code = "120", Description = "DTaP-Hib-IPV", SortOrder = 7 },
            new Vaccine { Id = 8, Code = "130", Description = "DTaP-IPV", SortOrder = 8 },
            new Vaccine { Id = 9, Code = "132", Description = "DTaP-IPV-Hib-Hep B, historical", SortOrder = 9 },
            new Vaccine { Id = 10, Code = "01", Description = "DTP", SortOrder = 10 },
            new Vaccine { Id = 11, Code = "22", Description = "DTP-Hib", SortOrder = 11 },
            new Vaccine { Id = 12, Code = "102", Description = "DTP-Hib-Hep B", SortOrder = 12 },
            new Vaccine { Id = 13, Code = "04", Description = "M/R (Measles-Rubella)", SortOrder = 13 },
            new Vaccine { Id = 14, Code = "05", Description = "Measles ONLY", SortOrder = 14 },
            new Vaccine { Id = 15, Code = "03", Description = "MMR", SortOrder = 15 },
            new Vaccine { Id = 16, Code = "94", Description = "MMRV", SortOrder = 16 },
            new Vaccine { Id = 17, Code = "07", Description = "Mumps ONLY", SortOrder = 17 },
            new Vaccine { Id = 18, Code = "998", Description = "No Vaccine Administered", SortOrder = 18 },
            new Vaccine { Id = 19, Code = "OTH", Description = "Other", SortOrder = 19 },
            new Vaccine { Id = 20, Code = "11", Description = "Pertussis ONLY", SortOrder = 20 },
            new Vaccine { Id = 21, Code = "06", Description = "Rubella ONLY", SortOrder = 21 },
            new Vaccine { Id = 22, Code = "38", Description = "R/M (Rubella-Mumps)", SortOrder = 22 },
            new Vaccine { Id = 23, Code = "115", Description = "Tdap", SortOrder = 23 },
            new Vaccine { Id = 24, Code = "999", Description = "Unknown", SortOrder = 24 },
            new Vaccine { Id = 25, Code = "21", Description = "Varicella", SortOrder = 25 });
    }
}
