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
/// This session writes what a specimen is and what it is collected from, and
/// no more. The legacy <c>lab</c> table also carries a provider, a diagnostic
/// facility and a reporting facility, each a bare id into the <c>facility</c>
/// table that does not exist here yet; none of them is here, for the reason
/// <see cref="LabFacilityId"/> and <see cref="SpecimenSourceId"/> are bare
/// nullable ints and not navigations. The direct <c>person_condition_id</c>
/// the legacy table carries is not here either: the specimen is linked to a
/// case morbidity report through <see cref="PersonConditionLab"/>, which is how
/// the session-011 snapshot links row 11 to report 14.
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
    /// The facility that performed the lab work, as the legacy
    /// <c>lab_facility_id</c>. A bare nullable id into the <c>facility</c>
    /// table, which does not exist here yet.
    /// </summary>
    public int? LabFacilityId { get; set; }

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
