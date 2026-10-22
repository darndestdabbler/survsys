using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="Condition"/> onto the <c>conditions</c> table, column by column
/// and under the legacy names.
/// </summary>
/// <remarks>
/// Internal, because nothing outside this module may say what the schema is;
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly.
///
/// The two bounded lengths leave room over what the legacy data holds — the
/// longest seeded name is 96 characters and every CDC code is five digits —
/// rather than sitting on it. The three lead-in columns are left unbounded,
/// because they hold paragraphs of investigator instructions and a cap there
/// would be a guess at prose.
/// </remarks>
internal sealed class ConditionConfiguration : IEntityTypeConfiguration<Condition>
{
    public void Configure(EntityTypeBuilder<Condition> builder)
    {
        builder.ToTable("conditions");
        builder.HasKey(condition => condition.Id);

        builder.Property(condition => condition.Id).HasColumnName("id");
        builder.Property(condition => condition.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(condition => condition.ContactLeadIn).HasColumnName("contact_lead_in");
        builder.Property(condition => condition.PlaceLeadIn).HasColumnName("place_lead_in");
        builder.Property(condition => condition.TreatmentLeadIn).HasColumnName("treatment_lead_in");
        builder.Property(condition => condition.Active).HasColumnName("active");
        builder.Property(condition => condition.CdcCode)
            .HasColumnName("cdc_code")
            .HasMaxLength(20);
        builder.Property(condition => condition.ConditionGroupId).HasColumnName("condition_group_id");
        builder.Property(condition => condition.BypassOutOfState).HasColumnName("bypass_out_of_state");
        builder.Property(condition => condition.BlockPreliminary).HasColumnName("block_preliminary");
    }
}
