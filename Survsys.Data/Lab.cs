namespace Survsys.Data;

/// <summary>
/// A laboratory specimen: the record one collection of material from a person
/// is known by, and the head of the chain a test and a result hang off.
/// </summary>
/// <remarks>
/// The columns are EpiTrax's own <c>lab</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, down to their snake_case names — so a later migration
/// of the legacy data reads straight across rather than through a column-name
/// translation nobody kept.
/// <c>../nedss/docs/db-snapshots/session-011/02-after-lab-save.txt</c> dumps
/// one such row: specimen <c>LAB-2026-011</c>, collected on the 8th of March,
/// from specimen source 12, at lab facility 11.
///
/// Every column of the legacy <c>lab</c> table is here. The performing lab, the
/// diagnostic facility and the reporting facility are each a bare nullable id
/// into the <c>facility</c> table, and the ordering provider one into the
/// <c>provider</c> table, which does not exist here yet — the "Performing
/// Lab", "Ordering Clinician" and "Ordering Facility" fields of the legacy Add
/// Lab form, as <c>../nedss/docs/functional/epitrax-cmr-deep-editing.md</c>
/// section 4 lists
/// them — held as plain ints and not navigations, for the reason
/// <see cref="SpecimenSourceId"/> is.
///
/// <see cref="PersonConditionId"/> is the legacy <c>person_condition_id</c>,
/// the report the specimen was recorded against. It is set where the specimen
/// is recorded, from the report in the route, and held as a plain nullable int
/// rather than a navigation. The enforced link is
/// <see cref="PersonConditionLab"/>, the join the legacy data populates and the
/// list reads; this column mirrors it for a reader that has a specimen in hand,
/// which is what the legacy schema does — the column and the join both exist,
/// and both are written together so neither can say a different report from the
/// other.
///
/// There is no behaviour here and no navigation out: nothing walks from a
/// specimen to its person, its tests or its report. The tests and results that
/// hang off a specimen are read by composing their own queries, and the
/// session that walks one adds the navigation it needs.
/// </remarks>
public class Lab
{
    public int Id { get; set; }

    /// <summary>The person this specimen was collected from.</summary>
    public int PersonId { get; set; }

    /// <summary>
    /// The case morbidity report this specimen was recorded against, as the
    /// legacy <c>person_condition_id</c>; null where none is set. A bare
    /// nullable id and not a navigation — the enforced link is the
    /// <see cref="PersonConditionLab"/> join — set where the specimen is
    /// recorded.
    /// </summary>
    public int? PersonConditionId { get; set; }

    /// <summary>
    /// The facility that performed the lab work, as the legacy
    /// <c>lab_facility_id</c> — the Add Lab form's "Performing Lab". A bare
    /// nullable id into the <c>facility</c> table.
    /// </summary>
    public int? LabFacilityId { get; set; }

    /// <summary>
    /// The clinician who ordered the lab work, as the legacy
    /// <c>provider_id</c> — the Add Lab form's "Ordering Clinician". A bare
    /// nullable id into the <c>provider</c> table, which does not exist here
    /// yet.
    /// </summary>
    public int? ProviderId { get; set; }

    /// <summary>
    /// The facility that performed the diagnosis, as the legacy
    /// <c>diagnostic_facility_id</c>. A bare nullable id into the
    /// <c>facility</c> table.
    /// </summary>
    public int? DiagnosticFacilityId { get; set; }

    /// <summary>
    /// The facility that reported the result, as the legacy
    /// <c>reporting_facility_id</c> — the Add Lab form's "Ordering Facility". A
    /// bare nullable id into the <c>facility</c> table.
    /// </summary>
    public int? ReportingFacilityId { get; set; }

    /// <summary>
    /// What the specimen is — blood, urine and the rest — as the legacy
    /// <c>specimen_source_id</c>. A bare nullable id into the
    /// <c>specimen_source</c> reference table, which does not exist here yet.
    /// </summary>
    public int? SpecimenSourceId { get; set; }

    /// <summary>
    /// The date the specimen was collected, where it is known.
    /// </summary>
    /// <remarks>
    /// A <see cref="DateOnly"/> where the legacy column is a <c>TIMESTAMP</c>,
    /// for the reason <see cref="Person.BirthDate"/> is one: the session-011
    /// value is at midnight and the lab form asks for it with a date picker.
    /// </remarks>
    public DateOnly? CollectionDate { get; set; }

    /// <summary>
    /// The accession number the performing lab knows the specimen by, where it
    /// has one — <c>LAB-2026-011</c> in the session-011 snapshot.
    /// </summary>
    public string? AccessionNo { get; set; }

    /// <summary>
    /// Whether the specimen was sent on to the state lab, where it is recorded.
    /// </summary>
    public bool? SpecimenSentToState { get; set; }
}
