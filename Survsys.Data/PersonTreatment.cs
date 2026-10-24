namespace Survsys.Data;

/// <summary>
/// A treatment record against a case: a treatment from the catalogue given to
/// the case's person, with its start and stop dates, dose and comment.
/// </summary>
/// <remarks>
/// The columns are EpiTrax's own <c>person_treatment</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, kept to what a case's record needs, with a dose the
/// legacy keeps in its comment. The legacy row hangs off <c>person_id</c> with
/// a <c>person_condition_id</c> beside it; this holds the record against the
/// case alone, as <see cref="PersonVaccine"/> does. The treatment is a real
/// foreign key into the catalogue that does not cascade, while the record is
/// deleted with its case.
/// </remarks>
public class PersonTreatment
{
    public int Id { get; set; }

    /// <summary>
    /// The case the treatment was recorded against. A real foreign key that
    /// cascades: the record is a detail of the case and goes with it.
    /// </summary>
    public int PersonConditionId { get; set; }

    /// <summary>
    /// The treatment given, as the legacy <c>treatment_id</c> — a real foreign
    /// key into the <see cref="Treatment"/> catalogue that does not cascade.
    /// </summary>
    public int TreatmentId { get; set; }

    /// <summary>
    /// The day the treatment started, as the legacy <c>treatment_date</c>; null
    /// where unrecorded. A <see cref="DateOnly"/> as the other dates on a case
    /// are, over a legacy <c>TIMESTAMP</c>.
    /// </summary>
    public DateOnly? TreatmentDate { get; set; }

    /// <summary>
    /// The day the treatment stopped, as the legacy
    /// <c>stop_treatment_date</c>; null where it has not stopped or is
    /// unrecorded.
    /// </summary>
    public DateOnly? StopTreatmentDate { get; set; }

    /// <summary>The dose given, as free text; null where unrecorded.</summary>
    public string? Dose { get; set; }

    /// <summary>A free-text note on the record, as the legacy <c>treatment_comment</c>.</summary>
    public string? Comment { get; set; }

    /// <summary>
    /// When the record was entered. A <see cref="DateTimeOffset"/> as
    /// <see cref="PersonVaccine.CreatedAt"/> is; set where the record is created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
