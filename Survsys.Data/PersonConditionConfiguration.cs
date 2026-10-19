using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Survsys.Data;

/// <summary>
/// <see cref="PersonCondition"/> onto the <c>person_conditions</c> table,
/// column by column and under the legacy names.
/// </summary>
/// <remarks>
/// Internal, for the reason every configuration here is: nothing outside this
/// module may say what the schema is, and
/// <see cref="SurvsysDbContext"/> finds it by scanning the assembly.
///
/// The table is <c>person_conditions</c> rather than the legacy singular, the
/// plural session 5 gave <c>conditions</c> and session 13 gave <c>persons</c>.
///
/// Two things here are tightened against what the legacy schema permits.
/// <see cref="PersonCondition.RecordNumber"/> is required and unique where
/// <c>record_number VARCHAR</c> was neither: a report without a number cannot
/// be referred to, and two reports sharing one cannot be told apart. And the
/// two foreign keys are required where the legacy columns are nullable in
/// name only — <c>person_id</c> and <c>condition_id</c> are the two things a
/// report is about, and the legacy data has no row missing either.
/// </remarks>
internal sealed class PersonConditionConfiguration : IEntityTypeConfiguration<PersonCondition>
{
    /// <summary>
    /// Room for a record number. Nine characters is the legacy
    /// <c>YYYYNNNNN</c> format; the bound leaves room over it rather than
    /// sitting on it, and bounding it at all is what lets it carry the unique
    /// index below — an unbounded string arrives on SQL Server as
    /// <c>nvarchar(max)</c>, which cannot be indexed.
    /// </summary>
    private const int RecordNumberLength = 20;

    /// <summary>
    /// Room for a short coded value: the Yes/No/Unknown the Clinical tab writes
    /// to <see cref="PersonCondition.TreatmentGiven"/>, the case status code on
    /// <see cref="PersonCondition.CaseStatus"/> whose longest value is the
    /// two-letter <c>NC</c>, and the workflow code on
    /// <see cref="PersonCondition.WorkflowState"/> whose longest value is the
    /// nineteen-character <c>under_investigation</c>. Session 24 adds the six
    /// Investigation and Administrative codes to the same bound: the four
    /// Contact Oriented Yes/No/Unknown codes, the two-letter transmission mode
    /// and the Yes/No/Unknown outbreak association, none longer than the
    /// seven-character <c>unknown</c>. The legacy columns are unbounded text; a
    /// column that holds a code does not need room for prose, and the bound
    /// leaves room over the longest value rather than sitting on it.
    /// </summary>
    private const int CodeLength = 20;

    public void Configure(EntityTypeBuilder<PersonCondition> builder)
    {
        builder.ToTable("person_conditions");
        builder.HasKey(report => report.Id);

        builder.Property(report => report.Id).HasColumnName("id");
        builder.Property(report => report.PersonId).HasColumnName("person_id");
        builder.Property(report => report.ConditionId).HasColumnName("condition_id");
        builder.Property(report => report.RecordNumber)
            .HasColumnName("record_number")
            .HasMaxLength(RecordNumberLength)
            .IsRequired();
        builder.Property(report => report.AgencyId).HasColumnName("agency_id");
        builder.Property(report => report.FirstReportedPhDate)
            .HasColumnName("first_reported_ph_date");
        builder.Property(report => report.EventOnsetDate).HasColumnName("event_onset_date");
        builder.Property(report => report.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        builder.Property(report => report.DiseaseOnsetDate)
            .HasColumnName("disease_onset_date");
        builder.Property(report => report.DateDiagnosed).HasColumnName("date_diagnosed");
        builder.Property(report => report.TreatmentGiven)
            .HasColumnName("treatment_given")
            .HasMaxLength(CodeLength);
        builder.Property(report => report.CaseStatus)
            .HasColumnName("case_status")
            .HasMaxLength(CodeLength);
        builder.Property(report => report.MmwrWeek).HasColumnName("mmwr_week");
        builder.Property(report => report.MmwrYear).HasColumnName("mmwr_year");
        builder.Property(report => report.WorkflowState)
            .HasColumnName("workflow_state")
            .HasMaxLength(CodeLength);
        builder.Property(report => report.InvestigatorId).HasColumnName("investigator_id");

        // The event type, added in session 62. Required, and defaulted in the
        // database as well as on the entity, so every row written before the
        // column existed, and every row a raw-SQL fixture inserts, reads as a
        // morbidity report.
        builder.Property(report => report.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(CodeLength)
            .IsRequired()
            .HasDefaultValue(EventTypes.Morbidity);

        // The Investigation tab's columns, added in session 24: the four
        // Contact Oriented codes, the imported-from key, the transmission-mode
        // code and the four free-text fields. The codes are bounded like every
        // code above; the free text is not, because the legacy columns are
        // unbounded and nothing indexes them.
        builder.Property(report => report.FoodHandler)
            .HasColumnName("food_handler")
            .HasMaxLength(CodeLength);
        builder.Property(report => report.HealthcareWorker)
            .HasColumnName("healthcare_worker")
            .HasMaxLength(CodeLength);
        builder.Property(report => report.GroupLiving)
            .HasColumnName("group_living")
            .HasMaxLength(CodeLength);
        builder.Property(report => report.DayCareAssociation)
            .HasColumnName("day_care_association")
            .HasMaxLength(CodeLength);
        builder.Property(report => report.ImportedFromId).HasColumnName("imported_from_id");
        builder.Property(report => report.TransmissionMode)
            .HasColumnName("transmission_mode")
            .HasMaxLength(CodeLength);
        builder.Property(report => report.RiskFactors).HasColumnName("risk_factors");
        builder.Property(report => report.RiskFactorsNotes).HasColumnName("risk_factors_notes");
        builder.Property(report => report.OtherData1).HasColumnName("other_data_1");
        builder.Property(report => report.OtherData2).HasColumnName("other_data_2");

        // The Administrative tab's columns, added in the same session: the two
        // per-jurisdiction case-status keys, the outbreak-association code, the
        // investigation dates, the outcome key, the event name and the two
        // reported dates. The three keys are bare identifiers with no
        // navigation, exactly as agency_id is above.
        builder.Property(report => report.LhdCaseStatusId).HasColumnName("lhd_case_status_id");
        builder.Property(report => report.StateCaseStatusId)
            .HasColumnName("state_case_status_id");
        builder.Property(report => report.OutbreakId).HasColumnName("outbreak_id");
        builder.Property(report => report.OutbreakAssociated)
            .HasColumnName("outbreak_associated")
            .HasMaxLength(CodeLength);
        builder.Property(report => report.LhdInvestigationStartDate)
            .HasColumnName("lhd_investigation_start_date");
        builder.Property(report => report.LhdInvestigationCompletedDate)
            .HasColumnName("lhd_investigation_completed_date");
        builder.Property(report => report.InvestigationOutcomeDispositionId)
            .HasColumnName("investigation_outcome_disposition_id");
        builder.Property(report => report.EventName).HasColumnName("event_name");
        builder.Property(report => report.ResultsReportedToClinicianDate)
            .HasColumnName("results_reported_to_clinician_date");
        builder.Property(report => report.CdcFirstVerballyNotifiedDate)
            .HasColumnName("cdc_first_verbally_notified_date");
        builder.Property(report => report.SentToIbis).HasColumnName("sent_to_ibis");
        builder.Property(report => report.IbisUpdatedAt).HasColumnName("ibis_updated_at");

        // What makes a record number a way of referring to a report. The rule
        // that issues one reads the highest already issued for the year and
        // adds to it, which two creations in the same instant can both do: the
        // second insert breaks on this index rather than quietly writing a
        // second report with the first one's number.
        builder.HasIndex(report => report.RecordNumber).IsUnique();

        // No navigation on either side, and none on Person or Condition:
        // nothing in this session walks from a report to the person it is
        // about, or from a person to their reports, and a navigation nothing
        // reads is a thing for the session that reads it to add.
        //
        // Deleting is refused rather than cascaded, which is the opposite of
        // what a person's addresses and telephones do and deliberately so: an
        // address is a detail of a person and goes when they do, but a case
        // morbidity report is the record itself. A person or a condition with
        // reports against it cannot be deleted out from under them.
        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(report => report.PersonId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Condition>()
            .WithMany()
            .HasForeignKey(report => report.ConditionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // The outbreak a case names is optional and does not cascade: a
        // deleted outbreak leaves its cases and nulls the column, so the
        // record outlives the outbreak it was grouped under rather than being
        // taken with it.
        builder.HasOne<Outbreak>()
            .WithMany()
            .HasForeignKey(report => report.OutbreakId)
            .OnDelete(DeleteBehavior.SetNull);

        // The investigator is optional and does not cascade: users are
        // deactivated rather than deleted, and a report keeps its investigator
        // on record whatever becomes of the account.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(report => report.InvestigatorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
