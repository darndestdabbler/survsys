using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="Treatment"/> onto the <c>treatments</c> table, column by column
/// and under the legacy <c>treatment</c> names.
/// </summary>
/// <remarks>
/// Internal, as every configuration here is: nothing outside this module says
/// what the schema is, and <see cref="SurvsysDbContext"/> finds it by scanning
/// the assembly. The table is <c>treatments</c>, the plural every table here is
/// given over the legacy singular. <see cref="Treatment.Name"/> is required and
/// bounded as <see cref="ConditionConfiguration"/> bounds a condition's name;
/// the two flags and the created stamp are required where the legacy columns
/// are <c>NOT NULL</c>.
/// </remarks>
internal sealed class TreatmentConfiguration : IEntityTypeConfiguration<Treatment>
{
    private const int NameLength = 200;

    public void Configure(EntityTypeBuilder<Treatment> builder)
    {
        builder.ToTable("treatments");
        builder.HasKey(treatment => treatment.Id);

        builder.Property(treatment => treatment.Id).HasColumnName("id");
        builder.Property(treatment => treatment.Name)
            .HasColumnName("treatment_name")
            .HasMaxLength(NameLength)
            .IsRequired();
        builder.Property(treatment => treatment.Active)
            .HasColumnName("active")
            .IsRequired();
        builder.Property(treatment => treatment.DefaultTreatment)
            .HasColumnName("default_treatment")
            .IsRequired();
        builder.Property(treatment => treatment.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
    }
}
