namespace Survsys.Data;

/// <summary>
/// An exposure: one event at which a person with a condition may have passed
/// it on — a place, a time range and a name — and the head of the chain the
/// contacts and the sources hang off.
/// </summary>
/// <remarks>
/// The columns are EpiTrax's own <c>exposure</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, down to their snake_case names — so a later migration
/// of the legacy data reads straight across rather than through a column-name
/// translation nobody kept. The screen it comes from is the Investigation
/// tab's "+ Exposure" panel and the Contacts tab that lists what it records,
/// as <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps them: the panel writes <c>exposure</c>, and the related
/// <c>exposure_contact</c>, <c>exposure_place</c> and <c>exposure_source</c>
/// tables hang off it.
///
/// One column of the legacy table is not here, and one that is here is not in
/// the legacy table. Legacy EpiTrax scopes an exposure by condition and agency
/// across a disease — every report of the same condition at the same agency
/// sees it — and ties it to an outbreak through <c>outbreak_id</c>. This
/// schema scopes an exposure to the one report it is recorded against,
/// through <see cref="PersonConditionId"/>, and omits <c>outbreak_id</c>:
/// outbreak management and outbreak-level sharing of an exposure are session
/// 50's, and a reference nothing else reads is not written for one release.
/// The legacy <c>condition_id</c> and <c>agency_id</c> are kept as the
/// columns they are, so the legacy data reads across, but they are nullable
/// here where the legacy <c>condition_id</c> is <c>NOT NULL</c>, because the
/// report is the scope and the disease is read off it.
///
/// The place, the condition and the agency are bare nullable ids and not
/// navigations, exactly as <see cref="Lab.SpecimenSourceId"/> is, and the
/// report is the plain nullable int <see cref="Lab.PersonConditionId"/> is.
/// There is no behaviour here and no navigation out: the contacts and sources
/// that hang off an exposure are read by composing their own queries, and the
/// session that walks one adds the navigation it needs.
/// </remarks>
public class Exposure
{
    public int Id { get; set; }

    /// <summary>
    /// The case morbidity report this exposure was recorded against, as the
    /// legacy <c>person_condition_id</c> column is named elsewhere in the
    /// legacy schema; null where none is set.
    /// </summary>
    /// <remarks>
    /// This column is this schema's own and not the legacy <c>exposure</c>
    /// table's: it is what scopes an exposure to one report, where legacy
    /// EpiTrax scopes it by condition and agency across a disease and ties it
    /// to an outbreak. The Contacts tab lists a report's exposures by it.
    /// Sharing an exposure across the reports of an outbreak is session 50. A
    /// bare nullable id and not a navigation, as <see cref="Lab.PersonConditionId"/>
    /// is, set where the exposure is recorded from the report in the route.
    /// </remarks>
    public int? PersonConditionId { get; set; }

    /// <summary>
    /// The disease the exposure is to, as the legacy <c>condition_id</c>. A
    /// bare nullable id into the <c>condition</c> table and not a navigation,
    /// as <see cref="PersonCondition.ConditionId"/>'s companions are; nullable
    /// here where the legacy column is not, because the report scopes the
    /// exposure and carries the condition.
    /// </summary>
    public int? ConditionId { get; set; }

    /// <summary>
    /// The agency that recorded the exposure, as the legacy <c>agency_id</c>.
    /// A bare nullable int, as <see cref="PersonCondition.AgencyId"/> is: there
    /// is no <c>agency</c> table in this schema to point at.
    /// </summary>
    public int? AgencyId { get; set; }

    /// <summary>
    /// The place the exposure happened, as the legacy <c>exposure_place_id</c>:
    /// the <see cref="ExposurePlace"/> row recorded with the exposure. A bare
    /// nullable int and not a navigation, held the way every coded id in the
    /// Clinical and Laboratory slices is.
    /// </summary>
    public int? ExposurePlaceId { get; set; }

    /// <summary>The first day of the exposure, where it is known.</summary>
    /// <remarks>
    /// A <see cref="DateOnly"/> where the legacy column is a <c>TIMESTAMP</c>,
    /// for the reason <see cref="Lab.CollectionDate"/> is one: the form asks
    /// for it with a date picker.
    /// </remarks>
    public DateOnly? ExposureStart { get; set; }

    /// <summary>The last day of the exposure, where it is known.</summary>
    /// <remarks>
    /// A <see cref="DateOnly"/> where the legacy column is a <c>TIMESTAMP</c>,
    /// for the reason <see cref="ExposureStart"/> is one.
    /// </remarks>
    public DateOnly? ExposureEnd { get; set; }

    /// <summary>
    /// What the exposure is called, as free text — left unbounded, because it
    /// holds whatever an investigator wrote.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// What happened, as free text — left unbounded, as <see cref="Name"/> is.
    /// </summary>
    public string? Description { get; set; }
}
