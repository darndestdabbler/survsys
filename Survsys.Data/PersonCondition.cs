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
/// legacy table has 67 columns, and the other 60 belong to the sessions that
/// show them — the workflow and the case status, the clinical and laboratory
/// fields, the investigation and administrative tabs, the queue, the outbreak
/// link and the CDC and IBIS export timestamps. None of them is here, because
/// a column nothing in this schema reads or writes would be a half-feature.
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
}
