using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="CoreElement"/> onto the <c>core_elements</c> table, seeded with
/// every editable field of the Clinical, Investigation and Administrative
/// tabs, each shown by default.
/// </summary>
/// <remarks>
/// A field keeps the legacy uid where the legacy seed in
/// <c>V0003__init.sql</c> has one for it (<c>disease_onset_date</c>,
/// <c>person_died</c>, <c>person_date_of_death</c>); every other uid is the
/// field's name in the same style. The uid is what the tabs name a field by,
/// so a seeded uid is never renamed.
/// </remarks>
internal sealed class CoreElementConfiguration : IEntityTypeConfiguration<CoreElement>
{
    public const string Clinical = "Clinical";
    public const string Investigation = "Investigation";
    public const string Administrative = "Administrative";

    public void Configure(EntityTypeBuilder<CoreElement> builder)
    {
        builder.ToTable("core_elements");
        builder.HasKey(element => element.Id);

        builder.Property(element => element.Id).HasColumnName("id");
        builder.Property(element => element.Uid)
            .HasColumnName("uid")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(element => element.Label)
            .HasColumnName("label")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(element => element.Tab)
            .HasColumnName("tab")
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(element => element.Hidden).HasColumnName("hidden");

        builder.HasIndex(element => element.Uid).IsUnique();

        builder.HasData(
            new CoreElement { Id = 1, Uid = "disease_onset_date", Label = "Disease onset date", Tab = Clinical },
            new CoreElement { Id = 2, Uid = "date_diagnosed", Label = "Date diagnosed", Tab = Clinical },
            new CoreElement { Id = 3, Uid = "treatment_given", Label = "Treatment given", Tab = Clinical },
            new CoreElement { Id = 4, Uid = "person_died", Label = "Died?", Tab = Clinical },
            new CoreElement { Id = 5, Uid = "person_date_of_death", Label = "Date of death", Tab = Clinical },
            new CoreElement { Id = 6, Uid = "food_handler", Label = "Food handler", Tab = Investigation },
            new CoreElement { Id = 7, Uid = "healthcare_worker", Label = "Health care worker", Tab = Investigation },
            new CoreElement { Id = 8, Uid = "group_living", Label = "Group living", Tab = Investigation },
            new CoreElement { Id = 9, Uid = "day_care_association", Label = "Day care association", Tab = Investigation },
            new CoreElement { Id = 10, Uid = "imported_from", Label = "Imported from", Tab = Investigation },
            new CoreElement { Id = 11, Uid = "transmission_mode", Label = "Transmission mode", Tab = Investigation },
            new CoreElement { Id = 12, Uid = "risk_factors", Label = "Risk factors", Tab = Investigation },
            new CoreElement { Id = 13, Uid = "risk_factors_notes", Label = "Risk factors notes", Tab = Investigation },
            new CoreElement { Id = 14, Uid = "other_data_1", Label = "Other data 1", Tab = Investigation },
            new CoreElement { Id = 15, Uid = "other_data_2", Label = "Other data 2", Tab = Investigation },
            new CoreElement { Id = 16, Uid = "lhd_case_status", Label = "LHD case status", Tab = Administrative },
            new CoreElement { Id = 17, Uid = "state_case_status", Label = "State case status", Tab = Administrative },
            new CoreElement { Id = 18, Uid = "outbreak_associated", Label = "Outbreak associated", Tab = Administrative },
            new CoreElement { Id = 19, Uid = "lhd_investigation_start_date", Label = "LHD investigation started", Tab = Administrative },
            new CoreElement { Id = 20, Uid = "lhd_investigation_completed_date", Label = "LHD investigation completed", Tab = Administrative },
            new CoreElement { Id = 21, Uid = "investigation_outcome", Label = "Investigation outcome", Tab = Administrative },
            new CoreElement { Id = 22, Uid = "event_name", Label = "Event name", Tab = Administrative },
            new CoreElement { Id = 23, Uid = "results_reported_to_clinician_date", Label = "Results reported to LHD", Tab = Administrative },
            new CoreElement { Id = 24, Uid = "cdc_first_verbally_notified_date", Label = "Date CDC verbally notified", Tab = Administrative });
    }
}
