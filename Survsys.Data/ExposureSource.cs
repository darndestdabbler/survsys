namespace Survsys.Data;

/// <summary>
/// A source of an exposure: who or what the condition came from at it — a
/// person on record, a report, or a name and a kind where nothing on record
/// fits.
/// </summary>
/// <remarks>
/// The columns are EpiTrax's own <c>exposure_source</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, down to their snake_case names, and the screen is
/// the Investigation tab's "+ Exposure" panel that
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps to <c>exposure</c> and its related <c>exposure_source</c>.
///
/// A source is a detail of its exposure, so <see cref="ExposureId"/> is
/// required and a source goes when its exposure does. The person and the
/// report it may name are bare nullable ids with no navigation and no foreign
/// key: a source may be a person the system has never seen, named only by
/// <see cref="SourceName"/>, and the legacy table holds both ids as plain
/// nullable columns. The source type is a plain code string, as the Clinical
/// and Laboratory slices hold their coded fields. There is no behaviour here
/// and no navigation out.
/// </remarks>
public class ExposureSource
{
    public int Id { get; set; }

    /// <summary>The exposure this is a source of.</summary>
    public int ExposureId { get; set; }

    /// <summary>
    /// The person the condition came from, where the source is a person on
    /// record, as the legacy <c>person_id</c>. A bare nullable int and not a
    /// navigation.
    /// </summary>
    public int? PersonId { get; set; }

    /// <summary>
    /// The case morbidity report the source is known by, where the source is a
    /// case on record, as the legacy <c>person_condition_id</c>. A bare nullable
    /// int and not a navigation, as <see cref="Lab.PersonConditionId"/> is.
    /// </summary>
    public int? PersonConditionId { get; set; }

    /// <summary>
    /// What the source is called, where it is named rather than referenced —
    /// a shortish string, not prose.
    /// </summary>
    public string? SourceName { get; set; }

    /// <summary>
    /// What kind of source this is, as a code; the legacy column is a plain
    /// <c>VARCHAR</c>.
    /// </summary>
    public string? SourceType { get; set; }
}
