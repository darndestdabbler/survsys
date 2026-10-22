namespace Survsys.Data;

/// <summary>
/// A healthcare encounter on a case: a dated visit, of a type, at a facility,
/// with a description.
/// </summary>
/// <remarks>
/// The columns are EpiTrax's own <c>encounter</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, kept to the date, the type, the facility and the
/// description. The legacy provider, recording user and person-facility keys
/// are left out. The type holds the legacy code with no table behind it, as
/// <see cref="PersonCondition.TransmissionMode"/> does. The facility is a real
/// foreign key that does not cascade, and the encounter is deleted with its
/// case.
/// </remarks>
public class Encounter
{
    public int Id { get; set; }

    /// <summary>
    /// The case the encounter belongs to. A real foreign key that cascades.
    /// </summary>
    public int PersonConditionId { get; set; }

    /// <summary>
    /// The day of the encounter, as the legacy <c>encounter_time</c>.
    /// Required: an encounter is a visit on a day. A <see cref="DateOnly"/> as
    /// the other dates on a case are.
    /// </summary>
    public DateOnly EncounterDate { get; set; }

    /// <summary>The kind of encounter, as the legacy <c>encounter_type</c> code; null where unrecorded.</summary>
    public string? EncounterType { get; set; }

    /// <summary>
    /// The facility the encounter was at, as the legacy <c>facility_id</c> — a
    /// foreign key into <see cref="Facility"/> that does not cascade; null where
    /// none is named.
    /// </summary>
    public int? FacilityId { get; set; }

    /// <summary>What happened, as the legacy <c>description</c> free text.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// When the encounter was entered; set where it is created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
