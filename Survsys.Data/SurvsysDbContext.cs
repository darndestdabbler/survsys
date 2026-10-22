using Microsoft.EntityFrameworkCore;

namespace Survsys.Data;

/// <summary>
/// The one way into the database. The schema is whatever the migrations in
/// this module say, and nothing outside this module creates or alters it.
/// </summary>
/// <remarks>
/// Entity configuration is not written here: it is picked up from every
/// <see cref="IEntityTypeConfiguration{TEntity}"/> in this assembly, so a new
/// entity arrives as its own configuration file rather than as another block
/// in a method that grows with the schema.
///
/// EF Core and SQL Server exceptions surface to the caller unwrapped — this
/// module never translates a failure into an HTTP concern, because it does
/// not know there is one.
/// </remarks>
public class SurvsysDbContext(DbContextOptions<SurvsysDbContext> options, IAuditUser? auditUser = null)
    : DbContext(options)
{
    /// <summary>The user the change audit names for this context's saves, if any.</summary>
    public int? AuditUserId => auditUser?.UserId;

    /// <summary>
    /// The last case this context's request classified and what the rules
    /// decided for it, so the request can answer with it; null until a case
    /// is classified.
    /// </summary>
    public (int ReportId, ClassificationOutcome Outcome)? Classified { get; set; }

    public DbSet<Condition> Conditions => Set<Condition>();

    public DbSet<Agency> Agencies => Set<Agency>();

    public DbSet<ZipCodeAgency> ZipCodeAgencies => Set<ZipCodeAgency>();

    public DbSet<ZipCode> ZipCodes => Set<ZipCode>();

    public DbSet<CtTown> CtTowns => Set<CtTown>();

    public DbSet<Facility> Facilities => Set<Facility>();

    public DbSet<CoreElement> CoreElements => Set<CoreElement>();

    public DbSet<CoreElementOverride> CoreElementOverrides => Set<CoreElementOverride>();

    public DbSet<Person> Persons => Set<Person>();

    public DbSet<PersonAddress> PersonAddresses => Set<PersonAddress>();

    public DbSet<PersonTelephone> PersonTelephones => Set<PersonTelephone>();

    public DbSet<PersonEmail> PersonEmails => Set<PersonEmail>();

    public DbSet<PersonRace> PersonRaces => Set<PersonRace>();

    public DbSet<PersonAlias> PersonAliases => Set<PersonAlias>();

    public DbSet<PersonIdentifier> PersonIdentifiers => Set<PersonIdentifier>();

    public DbSet<PersonNameHistory> PersonNameHistory => Set<PersonNameHistory>();

    public DbSet<ParentGuardian> ParentGuardians => Set<ParentGuardian>();

    public DbSet<PersonCondition> PersonConditions => Set<PersonCondition>();

    public DbSet<Lab> Labs => Set<Lab>();

    public DbSet<LabTest> LabTests => Set<LabTest>();

    public DbSet<LabTestResult> LabTestResults => Set<LabTestResult>();

    public DbSet<LabResistTest> LabResistTests => Set<LabResistTest>();

    public DbSet<PersonConditionLab> PersonConditionLabs => Set<PersonConditionLab>();

    public DbSet<PersonConditionShare> PersonConditionShares => Set<PersonConditionShare>();

    public DbSet<Exposure> Exposures => Set<Exposure>();

    public DbSet<ExposurePlace> ExposurePlaces => Set<ExposurePlace>();

    public DbSet<ExposureContact> ExposureContacts => Set<ExposureContact>();

    public DbSet<ExposureSource> ExposureSources => Set<ExposureSource>();

    public DbSet<Note> Notes => Set<Note>();

    public DbSet<CaseTask> Tasks => Set<CaseTask>();

    public DbSet<Attachment> Attachments => Set<Attachment>();

    public DbSet<AttachmentFile> AttachmentFiles => Set<AttachmentFile>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<UserAgency> UserAgencies => Set<UserAgency>();

    public DbSet<RolePrivilege> RolePrivileges => Set<RolePrivilege>();

    public DbSet<AccessRecord> AccessRecords => Set<AccessRecord>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public DbSet<SavedView> SavedViews => Set<SavedView>();

    public DbSet<VocabMasterLoinc> VocabMasterLoincs => Set<VocabMasterLoinc>();

    public DbSet<VocabMasterOrganism> VocabMasterOrganisms => Set<VocabMasterOrganism>();

    public DbSet<VocabChildLoinc> VocabChildLoincs => Set<VocabChildLoinc>();

    public DbSet<VocabMasterValue> VocabMasterValues => Set<VocabMasterValue>();

    public DbSet<VocabChildValue> VocabChildValues => Set<VocabChildValue>();

    public DbSet<VocabRule> VocabRules => Set<VocabRule>();

    public DbSet<VocabRuleCondition> VocabRuleConditions => Set<VocabRuleCondition>();

    public DbSet<GraylistSpoolEntry> GraylistSpool => Set<GraylistSpoolEntry>();

    public DbSet<StagedMessage> StagedMessages => Set<StagedMessage>();

    public DbSet<StagedMessageAudit> StagedMessageAudits => Set<StagedMessageAudit>();

    public DbSet<NotificationQueueEntry> NotificationQueue => Set<NotificationQueueEntry>();

    public DbSet<CdcExportLogEntry> CdcExportLog => Set<CdcExportLogEntry>();

    public DbSet<CdcExportLogDetail> CdcExportLogDetails => Set<CdcExportLogDetail>();

    public DbSet<ConditionPrintCriterion> ConditionPrintCriteria => Set<ConditionPrintCriterion>();

    public DbSet<ReconciliationCutoffSetting> ReconciliationCutoffs => Set<ReconciliationCutoffSetting>();

    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    public DbSet<EcrDocument> EcrDocuments => Set<EcrDocument>();

    public DbSet<EcrValidationFinding> EcrValidationFindings => Set<EcrValidationFinding>();

    public DbSet<EcrConditionMapping> EcrConditionMappings => Set<EcrConditionMapping>();

    public DbSet<EcrRrCondition> EcrRrConditions => Set<EcrRrCondition>();

    public DbSet<PhinVariable> PhinVariables => Set<PhinVariable>();

    public DbSet<NotificationGuide> NotificationGuides => Set<NotificationGuide>();

    public DbSet<ConditionDetail> ConditionDetails => Set<ConditionDetail>();

    public DbSet<GuidePhinVariable> GuidePhinVariables => Set<GuidePhinVariable>();

    public DbSet<GuideLink> GuideLinks => Set<GuideLink>();

    public DbSet<RuleSet> RuleSets => Set<RuleSet>();

    public DbSet<RuleSetConfig> RuleSetConfigs => Set<RuleSetConfig>();

    public DbSet<RuleSetField> RuleSetFields => Set<RuleSetField>();

    public DbSet<RuleSetData> RuleSetData => Set<RuleSetData>();

    public DbSet<RuleSetValueMap> RuleSetValueMaps => Set<RuleSetValueMap>();

    public DbSet<RuleSetExpression> RuleSetExpressions => Set<RuleSetExpression>();

    public DbSet<RuleSetExpressionCondition> RuleSetExpressionConditions =>
        Set<RuleSetExpressionCondition>();

    public DbSet<GuideVariableDefault> GuideVariableDefaults => Set<GuideVariableDefault>();

    public DbSet<GuideVariableValue> GuideVariables => Set<GuideVariableValue>();

    public DbSet<PhinValueSetConcept> PhinValueSets => Set<PhinValueSetConcept>();

    public DbSet<PhinUnitOfMeasure> PhinUnitsOfMeasure => Set<PhinUnitOfMeasure>();

    public DbSet<ConditionLabOverride> ConditionLabOverrides => Set<ConditionLabOverride>();

    public DbSet<HepLabGroup> HepLabGroups => Set<HepLabGroup>();

    public DbSet<HepLabGroupCondition> HepLabGroupConditions => Set<HepLabGroupCondition>();

    public DbSet<HepLabGroupTestType> HepLabGroupTestTypes => Set<HepLabGroupTestType>();

    public DbSet<Outbreak> Outbreaks => Set<Outbreak>();

    public DbSet<OutbreakAccess> OutbreakAccess => Set<OutbreakAccess>();

    public DbSet<ReportSection> ReportSections => Set<ReportSection>();

    public DbSet<OutbreakReportSection> OutbreakReportSections => Set<OutbreakReportSection>();

    public DbSet<OutbreakUpdate> OutbreakUpdates => Set<OutbreakUpdate>();

    public DbSet<Form> Forms => Set<Form>();

    public DbSet<FormSection> FormSections => Set<FormSection>();

    public DbSet<FormQuestion> FormQuestions => Set<FormQuestion>();

    public DbSet<FormQuestionOption> FormQuestionOptions => Set<FormQuestionOption>();

    public DbSet<FormAssignment> FormAssignments => Set<FormAssignment>();

    public DbSet<FormAnswer> FormAnswers => Set<FormAnswer>();

    public DbSet<Treatment> Treatments => Set<Treatment>();

    public DbSet<ConditionTreatment> ConditionTreatments => Set<ConditionTreatment>();

    public DbSet<Vaccine> Vaccines => Set<Vaccine>();

    public DbSet<PersonVaccine> PersonVaccines => Set<PersonVaccine>();

    public DbSet<PersonTreatment> PersonTreatments => Set<PersonTreatment>();

    public DbSet<Encounter> Encounters => Set<Encounter>();

    public DbSet<WorkflowHistoryEntry> WorkflowHistory => Set<WorkflowHistoryEntry>();

    public DbSet<CodeValue> CodeValues => Set<CodeValue>();

    public DbSet<CodeValueCondition> CodeValueConditions => Set<CodeValueCondition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SurvsysDbContext).Assembly);

    // The change audit rides on every context, the application's and the
    // suite's alike, so neither call site has to remember it. It stands in for
    // the legacy PostgreSQL row triggers; see AuditInterceptor.
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.AddInterceptors(new AuditInterceptor());

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RecordMessageStates();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RecordMessageStates();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Adds a <see cref="StagedMessageAudit"/> to the save for each staged
    /// message it inserts or moves to another review state, so the trail is
    /// written with the change or not at all. The bulk updates that bypass the
    /// change tracker write their own.
    /// </summary>
    private void RecordMessageStates()
    {
        ChangeTracker.DetectChanges();

        var now = DateTime.UtcNow;
        var entered = ChangeTracker.Entries<StagedMessage>()
            .Where(entry => entry.State == EntityState.Added
                || (entry.State == EntityState.Modified
                    && entry.Property(message => message.ReviewStatus).IsModified
                    && !Equals(
                        entry.Property(message => message.ReviewStatus).OriginalValue,
                        entry.Property(message => message.ReviewStatus).CurrentValue)))
            .Select(entry => entry.Entity)
            .ToList();

        foreach (var message in entered)
        {
            message.Audits.Add(StagedMessageAudit.Of(message, now));
        }
    }
}
