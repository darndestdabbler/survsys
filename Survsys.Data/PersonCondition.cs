namespace Survsys.Data;

/// <summary>
/// A case morbidity report: the record a public health investigator opens
/// about one person and one condition, and what the legacy two-step creation
/// flow writes.
/// </summary>
/// <remarks>
/// The columns are EpiTrax's own <c>person_condition</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, down to their snake_case names — so that a later
/// migration of the legacy data reads straight across rather than through a
/// column-name translation nobody kept.
///
/// The name is the legacy table's and not the report's, which is the one place
/// this entity reads worse than it could. Users call this a CMR — a case
/// morbidity report — and the screens that create one say so. The table is
/// called <c>person_condition</c> because a morbidity report is not the only
/// thing in it: a contact record, the kind phase 3 adds, is a row of this same
/// table distinguished by <c>person_condition_type_id</c>, and so is every
/// other way a person can be linked to a condition. An entity called
/// <c>MorbidityReport</c> would be the wrong name for half the rows the table
/// will hold, so it keeps the name of the thing it is.
///
/// This session writes what the legacy creation flow writes and no more, which
/// <c>../nedss/docs/db-snapshots/session-010/02-after-cmr-creation.txt</c>
/// shows exactly: the person, the condition, the record number, the
/// investigating agency, the date the condition was first reported to public
/// health, the event onset date and the moment the record was created. The
/// legacy table has 67 columns, and the rest belong to the sessions that
/// show them — the workflow, the investigation and administrative tabs, the
/// queue, the outbreak link and the CDC and IBIS export timestamps — because a
/// column nothing in this schema reads or writes would be a half-feature.
///
/// Three of those fields are here as of session 18:
/// <see cref="DiseaseOnsetDate"/>, <see cref="DateDiagnosed"/> and
/// <see cref="TreatmentGiven"/>, the case-specific fields the legacy Clinical
/// tab writes, which
/// <c>../nedss/docs/functional/epitrax-cmr-deep-editing.md</c> section 3 maps.
/// Session 14 deferred them to the session that shows them, and this is the
/// schema half of it; the Clinical tab's person-level fields are on
/// <see cref="Person"/>, and the laboratory fields are their own entities
/// rather than columns here.
///
/// Three more are here as of session 20: <see cref="CaseStatus"/>, the case
/// classification, and <see cref="MmwrWeek"/> with <see cref="MmwrYear"/>, the
/// epidemiological week the report is counted in. They are a report's own
/// columns and no reference table stands behind the status, exactly as none
/// stands behind <see cref="AgencyId"/>: the status holds the legacy code and
/// the vocabulary lives in <see cref="CaseStatusRules"/> rather than in a
/// table, and the two MMWR columns are the arithmetic
/// <see cref="MmwrWeek.Of(System.DateOnly)"/> works over the event onset date.
///
/// Nineteen more are here as of session 24: the columns the legacy
/// Investigation and Administrative tabs write, which
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps field by field and
/// <c>../nedss/docs/db-snapshots/session-012/03-after-investigation-fill.txt</c>
/// and <c>05-after-admin-fill.txt</c> dump the values of. They are grouped
/// below in the order those tabs show them: the four Contact Oriented codes,
/// then the Investigation tab's Other section, then the Administrative tab's
/// Case / Outbreak, Auditing / Investigation and Reported Dates sections.
/// Every one is nullable, because every one is blank until an investigator
/// fills it in and the legacy columns are all nullable. The foreign keys among
/// them stay plain identifiers as <see cref="AgencyId"/> is, and the coded
/// fields hold the legacy code as <see cref="TreatmentGiven"/> does; the
/// reporter, the outbreak link, the queue, the tags, the occupations, the
/// exposures, the reporting criteria and the associated forms those tabs also
/// show are each a table of their own and are not here.
///
/// There is no behaviour here and no navigation out: nothing in this session
/// walks from a report to its person or its condition, so the two foreign keys
/// are the plain identifiers the configuration makes them, and the session
/// that walks one adds the navigation it needs.
/// </remarks>
public class PersonCondition
{
    public int Id { get; set; }

    /// <summary>The person this report is about.</summary>
    public int PersonId { get; set; }

    /// <summary>The disease or condition this report is for.</summary>
    public int ConditionId { get; set; }

    /// <summary>
    /// The number this report is known by, in the legacy <c>YYYYNNNNN</c>
    /// format: the year it was created in, then the sequence within that year
    /// padded to five digits, so the first report of 2026 is
    /// <c>202600001</c>.
    /// </summary>
    /// <remarks>
    /// Required, and unique across the table — which is the configuration's
    /// to say and the reason the rule that issues one can read the highest
    /// already issued and add to it. The caller never chooses it: it is
    /// assigned where a report is created, because a caller that could choose
    /// one could choose one already taken.
    /// </remarks>
    public required string RecordNumber { get; set; }

    /// <summary>
    /// The agency investigating this report, as the legacy
    /// <c>agency_id</c>.
    /// </summary>
    /// <remarks>
    /// A bare nullable identifier rather than a navigation, exactly as
    /// <see cref="Condition.ConditionGroupId"/> and
    /// <see cref="PersonAddress.ResidenceTypeId"/> are: the legacy
    /// <c>agency</c> table does not exist here yet, and a navigation to a
    /// table that does not exist is not a thing to write early. Nullable
    /// because the legacy column is.
    /// </remarks>
    public int? AgencyId { get; set; }

    /// <summary>
    /// The date the condition was first reported to public health — the first
    /// of the three fields step two of the creation flow asks for.
    /// </summary>
    /// <remarks>
    /// A <see cref="DateOnly"/> where the legacy column is a
    /// <c>TIMESTAMP</c>, for the reason <see cref="Person.BirthDate"/> gives:
    /// every value in <c>../nedss/docs/db-snapshots/session-010/</c> is at
    /// midnight, and the legacy screen asks for it with a date picker.
    /// </remarks>
    public DateOnly? FirstReportedPhDate { get; set; }

    /// <summary>
    /// The date the event this report is about began.
    /// </summary>
    /// <remarks>
    /// The legacy creation flow sets this to the date the record was created
    /// and leaves the real onset date to be entered later, which is what the
    /// snapshot shows: <c>event_onset_date</c> and <c>created_at</c> are the
    /// same day there. The Clinical tab is where an investigator corrects it,
    /// and that tab is a later session's.
    ///
    /// A <see cref="DateOnly"/> where the legacy column is a
    /// <c>TIMESTAMP</c>, for the reason <see cref="FirstReportedPhDate"/> is.
    /// </remarks>
    public DateOnly? EventOnsetDate { get; set; }

    /// <summary>
    /// When this report was created, and so the year its
    /// <see cref="RecordNumber"/> is taken from.
    /// </summary>
    /// <remarks>
    /// A <see cref="DateTimeOffset"/> and not a <see cref="DateOnly"/>,
    /// because this one is a moment rather than a day — the legacy column
    /// holds a time of day and the snapshot's value is
    /// <c>2026-03-12 22:07:16</c> — and it carries its offset, so a record
    /// created in Utah says which hour it was created in rather than leaving a
    /// reader to assume the server's zone.
    ///
    /// Required, and set where the report is created. It is not an audit
    /// column: nothing maintains it afterwards, nothing says who created the
    /// record, and the legacy <c>updated_at</c> that would pair with it is not
    /// here. Who did what is the access audit's, and that is a later
    /// session's.
    /// </remarks>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// The date the disease began in this person, as the Clinical tab records
    /// it; null until it has been entered.
    /// </summary>
    /// <remarks>
    /// A Clinical-tab field added in session 18, mapped by
    /// <c>../nedss/docs/functional/epitrax-cmr-deep-editing.md</c> section 3.
    /// Distinct from <see cref="EventOnsetDate"/>, which the creation flow set
    /// to the creation date: the Clinical tab is where the real onset is
    /// entered, and this is the column it writes. A <see cref="DateOnly"/>
    /// where the legacy column is a <c>DATE</c>, entered with a date picker as
    /// <see cref="FirstReportedPhDate"/> and <see cref="EventOnsetDate"/> are.
    /// </remarks>
    public DateOnly? DiseaseOnsetDate { get; set; }

    /// <summary>
    /// The date this case was diagnosed, as the Clinical tab records it; null
    /// until it has been entered.
    /// </summary>
    /// <remarks>
    /// A Clinical-tab field added in session 18, mapped by
    /// <c>../nedss/docs/functional/epitrax-cmr-deep-editing.md</c> section 3. A
    /// <see cref="DateOnly"/> where the legacy column is a <c>DATE</c>, for the
    /// reason <see cref="DiseaseOnsetDate"/> is one.
    /// </remarks>
    public DateOnly? DateDiagnosed { get; set; }

    /// <summary>
    /// Whether treatment was given for this case, as the legacy
    /// <c>treatment_given</c> code — one of <c>Yes</c>, <c>No</c> or
    /// <c>Unknown</c>, and null until it has been answered.
    /// </summary>
    /// <remarks>
    /// A Clinical-tab field added in session 18, mapped by
    /// <c>../nedss/docs/functional/epitrax-cmr-deep-editing.md</c> section 3,
    /// which notes this is case-specific and so lives on the report rather than
    /// on the person. A plain string holding the legacy code, because the
    /// reference table behind it does not exist here yet. The facility,
    /// clinician and treatment-detail rows the legacy "Add" dialogs create are
    /// a later session's; this is the basic Yes/No/Unknown answer and no more.
    /// </remarks>
    public string? TreatmentGiven { get; set; }

    /// <summary>
    /// The case classification: the legacy status code, or null for an
    /// unclassified case, which <see cref="CaseStatusRules"/> reads as Unknown.
    /// </summary>
    /// <remarks>
    /// The vocabulary is <see cref="CaseStatusRules"/>'s, the five statuses
    /// <c>../nedss/docs/business-logic/CLASSIFICATION_RULES.md</c> section 1.2
    /// lists reduced to the ones the session plan names — Confirmed, Probable,
    /// Suspected, Not a Case and Unknown. A plain string holding the legacy
    /// code, because there is no case-status reference table in this schema and
    /// the codes are a fixed CDC vocabulary rather than an
    /// installation-configurable list; it is set manually, since the
    /// disease-specific auto-classification that would assign it is the
    /// deferred, Utah-specific work section 8 describes. Null until a status is
    /// chosen, which is how the legacy leaves an unclassified case.
    /// </remarks>
    public string? CaseStatus { get; set; }

    /// <summary>
    /// The MMWR week the report's event is counted in, or null where it has no
    /// event onset date to count from.
    /// </summary>
    /// <remarks>
    /// Computed from <see cref="EventOnsetDate"/> by
    /// <see cref="MmwrWeek.Of(System.DateOnly)"/> and stored when the report is
    /// opened, as <see cref="RecordNumber"/> is assigned there: no column reads
    /// the event onset date on the fly, and the session that makes that date
    /// editable recomputes this. Paired with <see cref="MmwrYear"/>, because an
    /// MMWR week number means nothing without the year whose week it is — the
    /// days around a year end belong to the neighbouring MMWR year.
    /// </remarks>
    public int? MmwrWeek { get; set; }

    /// <summary>
    /// The MMWR year the report's event is counted in, which can differ from
    /// the event onset date's calendar year; null where there is no onset date.
    /// </summary>
    /// <remarks>
    /// Computed and stored with the MMWR week and from the same
    /// <see cref="EventOnsetDate"/>; the two are set and read together.
    /// </remarks>
    public int? MmwrYear { get; set; }

    /// <summary>
    /// Where this report sits in the investigation lifecycle: the legacy
    /// workflow code, or null for a report read as the assigned-to-LHD start.
    /// </summary>
    /// <remarks>
    /// The vocabulary and the state machine over it are
    /// <see cref="WorkflowRules"/>'s: the four states the session plan names —
    /// assigned to LHD, accepted, under investigation and closed — as the
    /// forward chain, with the codes a report's workflow may hold and the rule
    /// for moving between them. A plain string holding the legacy code, because
    /// there is no workflow reference table in this schema and
    /// <c>current_workflow_id</c> is a foreign key to one the legacy has and
    /// this does not, exactly as <see cref="CaseStatus"/> holds the status code
    /// and <see cref="AgencyId"/> references a list no table stands behind.
    ///
    /// A report is opened at <see cref="WorkflowRules.InitialCode"/>, as the
    /// legacy starts a case at the Local Health Department; null until then,
    /// which <see cref="WorkflowRules.Read(string?)"/> reads as that same
    /// assigned-to-LHD default, so a report written before this column existed
    /// is where it was. The move from one state to the next is the transition
    /// endpoint's, gated by <see cref="WorkflowRules.IsAllowed"/>; nothing here
    /// moves it on a report's labs or classification, because the
    /// classification-gated transitions are the deferred, Utah-specific work.
    /// </remarks>
    public string? WorkflowState { get; set; }

    /// <summary>
    /// Whether the person handles food, as the legacy <c>food_handler</c>
    /// code — the Investigation tab's first Contact Oriented dropdown, one of
    /// <c>yes</c>, <c>no</c> or <c>unknown</c>, and null until answered.
    /// </summary>
    /// <remarks>
    /// Mapped by
    /// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>,
    /// Investigation tab, Field-to-DB Mapping. A plain string holding the
    /// legacy code as <see cref="TreatmentGiven"/> does: the legacy column is a
    /// bare <c>VARCHAR</c> with no reference table behind it, and the value is
    /// the dropdown's lowercase code —
    /// <c>../nedss/docs/db-snapshots/session-012/03-after-investigation-fill.txt</c>
    /// shows <c>unknown</c> written to report 12 after the fill, where the
    /// screen had shown "Unknown".
    /// </remarks>
    public string? FoodHandler { get; set; }

    /// <summary>
    /// Whether the person works in health care, as the legacy
    /// <c>healthcare_worker</c> code — the second Contact Oriented dropdown,
    /// one of <c>yes</c>, <c>no</c> or <c>unknown</c>, and null until answered.
    /// </summary>
    /// <remarks>
    /// Mapped by
    /// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>,
    /// Investigation tab, Field-to-DB Mapping, and dumped as <c>unknown</c> in
    /// <c>../nedss/docs/db-snapshots/session-012/03-after-investigation-fill.txt</c>.
    /// A plain string holding the legacy code, for the reason
    /// <see cref="FoodHandler"/> is one.
    /// </remarks>
    public string? HealthcareWorker { get; set; }

    /// <summary>
    /// Whether the person lives in a group setting, as the legacy
    /// <c>group_living</c> code — the third Contact Oriented dropdown, one of
    /// <c>yes</c>, <c>no</c> or <c>unknown</c>, and null until answered.
    /// </summary>
    /// <remarks>
    /// Mapped by
    /// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>,
    /// Investigation tab, Field-to-DB Mapping, and dumped as <c>unknown</c> in
    /// <c>../nedss/docs/db-snapshots/session-012/03-after-investigation-fill.txt</c>.
    /// A plain string holding the legacy code, for the reason
    /// <see cref="FoodHandler"/> is one.
    /// </remarks>
    public string? GroupLiving { get; set; }

    /// <summary>
    /// Whether the person is associated with a day care, as the legacy
    /// <c>day_care_association</c> code — the fourth Contact Oriented
    /// dropdown, one of <c>yes</c>, <c>no</c> or <c>unknown</c>, and null until
    /// answered.
    /// </summary>
    /// <remarks>
    /// Mapped by
    /// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>,
    /// Investigation tab, Field-to-DB Mapping, and dumped as <c>unknown</c> in
    /// <c>../nedss/docs/db-snapshots/session-012/03-after-investigation-fill.txt</c>.
    /// A plain string holding the legacy code, for the reason
    /// <see cref="FoodHandler"/> is one.
    /// </remarks>
    public string? DayCareAssociation { get; set; }

    /// <summary>
    /// Where the condition was imported from, as the legacy
    /// <c>imported_from_id</c> — the Investigation tab's "Imported from"
    /// dropdown, whose legacy options are Utah, Outside U.S., Other U.S. State
    /// and Unknown; null until chosen.
    /// </summary>
    /// <remarks>
    /// Mapped by
    /// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>,
    /// Investigation tab, Field-to-DB Mapping, as a foreign key to the legacy
    /// <c>imported_from</c> table. A bare nullable identifier rather than a
    /// navigation, exactly as <see cref="AgencyId"/> is: that table does not
    /// exist here, and a navigation to a table that does not exist is not a
    /// thing to write early.
    /// <c>../nedss/docs/db-snapshots/session-012/03-after-investigation-fill.txt</c>
    /// shows <c>11</c> written to report 12 by the fill.
    /// </remarks>
    public int? ImportedFromId { get; set; }

    /// <summary>
    /// How the condition was transmitted, as the legacy
    /// <c>transmission_mode</c> code — the Investigation tab's "Transmission
    /// mode" dropdown, whose fourteen legacy options run from Airborne to
    /// Water-borne; null until chosen.
    /// </summary>
    /// <remarks>
    /// Mapped by
    /// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>,
    /// Investigation tab, Field-to-DB Mapping, which lists it as a lookup. The
    /// legacy column is <c>TEXT</c> referencing the code column of a
    /// <c>transmission_mode</c> table, so what the row holds is the code and
    /// not an identifier:
    /// <c>../nedss/docs/db-snapshots/session-012/03-after-investigation-fill.txt</c>
    /// shows <c>AB</c>, Airborne, written to report 12. A plain string holding
    /// that code, because the table behind it does not exist here, as
    /// <see cref="CaseStatus"/> holds its code with no table behind it.
    /// </remarks>
    public string? TransmissionMode { get; set; }

    /// <summary>
    /// The risk factors noted for this case, as the legacy
    /// <c>risk_factors</c> free text — the Investigation tab's "Risk factors"
    /// field; null until entered.
    /// </summary>
    /// <remarks>
    /// Mapped by
    /// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>,
    /// Investigation tab, Field-to-DB Mapping, as free text, which the legacy
    /// <c>text</c> column confirms. Unbounded for that reason. The session-012
    /// fill left it empty, which is what every row of
    /// <c>../nedss/docs/db-snapshots/session-012/03-after-investigation-fill.txt</c>
    /// shows.
    /// </remarks>
    public string? RiskFactors { get; set; }

    /// <summary>
    /// Notes on the risk factors, as the legacy <c>risk_factors_notes</c>
    /// free text — the Investigation tab's "Risk factors notes" textarea;
    /// null until entered.
    /// </summary>
    /// <remarks>
    /// Mapped by
    /// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>,
    /// Investigation tab, Field-to-DB Mapping, as a textarea over a legacy
    /// <c>text</c> column. Unbounded, as <see cref="RiskFactors"/> is, and left
    /// empty by the session-012 fill in
    /// <c>../nedss/docs/db-snapshots/session-012/03-after-investigation-fill.txt</c>.
    /// </remarks>
    public string? RiskFactorsNotes { get; set; }

    /// <summary>
    /// The first of the two catch-all fields, as the legacy
    /// <c>other_data_1</c> free text — the Investigation tab's "Other data 1";
    /// null until entered.
    /// </summary>
    /// <remarks>
    /// Mapped by
    /// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>,
    /// Investigation tab, Field-to-DB Mapping, as free text over a legacy
    /// unbounded <c>VARCHAR</c>. Unbounded here too: a field whose name says
    /// nothing about its contents cannot say how long they are. Left empty by
    /// the session-012 fill in
    /// <c>../nedss/docs/db-snapshots/session-012/03-after-investigation-fill.txt</c>.
    /// </remarks>
    public string? OtherData1 { get; set; }

    /// <summary>
    /// The second catch-all field, as the legacy <c>other_data_2</c> free
    /// text — the Investigation tab's "Other data 2"; null until entered.
    /// </summary>
    /// <remarks>
    /// Mapped by
    /// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>,
    /// Investigation tab, Field-to-DB Mapping, and unbounded for the reason
    /// <see cref="OtherData1"/> is. Left empty by the session-012 fill in
    /// <c>../nedss/docs/db-snapshots/session-012/03-after-investigation-fill.txt</c>.
    /// </remarks>
    public string? OtherData2 { get; set; }

    /// <summary>
    /// The local health department's case status, as the legacy
    /// <c>lhd_case_status_id</c> — the Administrative tab's "LHD case status"
    /// dropdown; null until chosen.
    /// </summary>
    /// <remarks>
    /// Mapped by
    /// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>,
    /// Administrative tab, Field-to-DB Mapping, as a foreign key to the legacy
    /// <c>case_status</c> table. Distinct from <see cref="CaseStatus"/>, which
    /// is the one classification survsys models and holds a code: this and
    /// <see cref="StateCaseStatusId"/> are the legacy's two per-jurisdiction
    /// status columns, kept as the identifiers the legacy writes so that the
    /// legacy data reads straight across. A bare nullable identifier rather
    /// than a navigation, exactly as <see cref="AgencyId"/> is, because the
    /// table does not exist here.
    /// <c>../nedss/docs/db-snapshots/session-012/05-after-admin-fill.txt</c>
    /// shows <c>11</c> written to report 12 by the fill.
    /// </remarks>
    public int? LhdCaseStatusId { get; set; }

    /// <summary>
    /// The state's case status, as the legacy <c>state_case_status_id</c> —
    /// the Administrative tab's "State case status" dropdown; null until
    /// chosen.
    /// </summary>
    /// <remarks>
    /// Mapped by
    /// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>,
    /// Administrative tab, Field-to-DB Mapping, as the second foreign key to
    /// the legacy <c>case_status</c> table, and a bare nullable identifier for
    /// the reason <see cref="LhdCaseStatusId"/> is one.
    /// <c>../nedss/docs/db-snapshots/session-012/05-after-admin-fill.txt</c>
    /// shows <c>11</c> written to report 12 by the fill.
    /// </remarks>
    public int? StateCaseStatusId { get; set; }

    /// <summary>
    /// Whether this case is associated with an outbreak, as the legacy
    /// <c>outbreak_associated</c> code — the Administrative tab's "Outbreak
    /// associated" dropdown, one of <c>Yes</c>, <c>No</c> or <c>Unknown</c>,
    /// and null until answered.
    /// </summary>
    /// <remarks>
    /// Mapped by
    /// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>,
    /// Administrative tab, Field-to-DB Mapping. The legacy column is bare
    /// <c>text</c> with no reference table behind it, so this is a plain
    /// string holding the code as <see cref="TreatmentGiven"/> does. The code
    /// is capitalised where the Contact Oriented codes are lowercase, which is
    /// the legacy's inconsistency and not this schema's to correct:
    /// <c>../nedss/docs/db-snapshots/session-012/05-after-admin-fill.txt</c>
    /// shows <c>Unknown</c> written to report 12 where the same fill wrote
    /// <c>unknown</c> to <see cref="FoodHandler"/>. The outbreak itself — the
    /// "Outbreak name" dropdown and its <c>outbreak_id</c> — is not here; that
    /// is outbreak management's, a later session.
    /// </remarks>
    public string? OutbreakAssociated { get; set; }

    /// <summary>
    /// The date the local health department started its investigation, as
    /// the legacy <c>lhd_investigation_start_date</c> — the Administrative
    /// tab's "LHD investigation started" date picker; null until entered.
    /// </summary>
    /// <remarks>
    /// Mapped by
    /// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>,
    /// Administrative tab, Field-to-DB Mapping. A <see cref="DateOnly"/> where
    /// the legacy column is a <c>TIMESTAMP</c>, for the reason
    /// <see cref="FirstReportedPhDate"/> is one: the legacy screen asks for it
    /// with a date picker, and a day entered that way has no time of day to
    /// keep. The session-012 fill left it empty, which is what
    /// <c>../nedss/docs/db-snapshots/session-012/05-after-admin-fill.txt</c>
    /// shows.
    /// </remarks>
    public DateOnly? LhdInvestigationStartDate { get; set; }

    /// <summary>
    /// The date the local health department completed its investigation, as
    /// the legacy <c>lhd_investigation_completed_date</c> — the
    /// Administrative tab's "LHD investigation completed" date picker; null
    /// until entered.
    /// </summary>
    /// <remarks>
    /// Mapped by
    /// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>,
    /// Administrative tab, Field-to-DB Mapping. A <see cref="DateOnly"/> over a
    /// legacy <c>TIMESTAMP</c>, for the reason
    /// <see cref="LhdInvestigationStartDate"/> is one, and left empty by the
    /// session-012 fill in
    /// <c>../nedss/docs/db-snapshots/session-012/05-after-admin-fill.txt</c>.
    /// </remarks>
    public DateOnly? LhdInvestigationCompletedDate { get; set; }

    /// <summary>
    /// The outcome of the investigation, as the legacy
    /// <c>investigation_outcome_disposition_id</c> — the Administrative tab's
    /// "Investigation outcome" dropdown; null until chosen.
    /// </summary>
    /// <remarks>
    /// Mapped by
    /// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>,
    /// Administrative tab, Field-to-DB Mapping, as a foreign key to the legacy
    /// <c>disposition</c> table. A bare nullable identifier rather than a
    /// navigation, exactly as <see cref="AgencyId"/> is, because that table
    /// does not exist here.
    /// <c>../nedss/docs/db-snapshots/session-012/05-after-admin-fill.txt</c>
    /// shows <c>11</c> written to report 12 by the fill.
    /// </remarks>
    public int? InvestigationOutcomeDispositionId { get; set; }

    /// <summary>
    /// A name for the event this report is about, as the legacy
    /// <c>event_name</c> free text — the Administrative tab's "Event name"
    /// field; null until entered.
    /// </summary>
    /// <remarks>
    /// Mapped by
    /// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>,
    /// Administrative tab, Field-to-DB Mapping, as free text over a legacy
    /// <c>text</c> column, and unbounded for that reason. Left empty by the
    /// session-012 fill in
    /// <c>../nedss/docs/db-snapshots/session-012/05-after-admin-fill.txt</c>.
    /// </remarks>
    public string? EventName { get; set; }

    /// <summary>
    /// The date results were reported to the clinician, as the legacy
    /// <c>results_reported_to_clinician_date</c> — the Administrative tab's
    /// "Results reported to LHD" date picker, under the Reported Dates
    /// section; null until entered.
    /// </summary>
    /// <remarks>
    /// Mapped by
    /// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>,
    /// Administrative tab, Field-to-DB Mapping, whose label and column name
    /// disagree about who was reported to; the column keeps the legacy name
    /// so that the legacy data reads straight across. A <see cref="DateOnly"/>
    /// over a legacy <c>TIMESTAMP</c>, for the reason
    /// <see cref="LhdInvestigationStartDate"/> is one, and left empty by the
    /// session-012 fill in
    /// <c>../nedss/docs/db-snapshots/session-012/05-after-admin-fill.txt</c>.
    /// </remarks>
    public DateOnly? ResultsReportedToClinicianDate { get; set; }

    /// <summary>
    /// The date the CDC was first verbally notified, as the legacy
    /// <c>cdc_first_verbally_notified_date</c> — the Administrative tab's
    /// "Date CDC verbally notified" date picker, under the Reported Dates
    /// section; null until entered.
    /// </summary>
    /// <remarks>
    /// Mapped by
    /// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>,
    /// Administrative tab, Field-to-DB Mapping, which leaves the column "TBD";
    /// the column is the one the snapshot's header carries under that name,
    /// which
    /// <c>../nedss/docs/db-snapshots/session-012/05-after-admin-fill.txt</c>
    /// shows empty for every row after the fill. A <see cref="DateOnly"/>,
    /// which here matches the legacy column exactly: this one was added to the
    /// legacy as a <c>DATE</c> rather than a <c>TIMESTAMP</c>.
    /// </remarks>
    public DateOnly? CdcFirstVerballyNotifiedDate { get; set; }
}
