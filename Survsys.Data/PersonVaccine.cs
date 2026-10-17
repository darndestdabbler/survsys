namespace Survsys.Data;

/// <summary>
/// A vaccine record against a case: a vaccine administered to the case's person,
/// with the dose, dates, lot number and comment the record carries.
/// </summary>
/// <remarks>
/// The columns are EpiTrax's own <c>person_vaccine</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0169__create_vaccine_tables.sql</c>
/// under <c>../nedss</c>, kept to the lean set a case's record needs: the
/// vaccine, its dose number, the administered and expiration dates, a lot
/// number and a comment. The legacy manufacturer and information-source foreign
/// keys and the export and external-identity columns
/// (<c>national_drug_code</c>, <c>vaccination_record_identifier</c>,
/// <c>data_source</c>) are left out.
///
/// The legacy row hangs off <c>person_id</c> and reaches a case through the
/// <c>person_condition_person_vaccine</c> join; this holds the record against
/// the case directly through <see cref="PersonConditionId"/>, as the other
/// case-scoped records do, so there is no join table. The vaccine is a real
/// foreign key into the catalogue that does not cascade — the catalogue is
/// protected — while the record itself is deleted with its case.
/// </remarks>
public class PersonVaccine
{
    public int Id { get; set; }

    /// <summary>
    /// The case the vaccine was recorded against — the
    /// <c>person_condition</c> this record belongs to. A real foreign key that
    /// cascades: the record is a detail of the case and goes with it.
    /// </summary>
    public int PersonConditionId { get; set; }

    /// <summary>
    /// The vaccine administered, as the legacy <c>vaccine_id</c> — a real
    /// foreign key into the <see cref="Vaccine"/> catalogue that does not
    /// cascade.
    /// </summary>
    public int VaccineId { get; set; }

    /// <summary>
    /// The day the vaccine was administered, as the legacy
    /// <c>administered_date</c>; null until given. A <see cref="DateOnly"/> as
    /// the other dates on a case are, over a legacy <c>date</c>.
    /// </summary>
    public DateOnly? AdministeredDate { get; set; }

    /// <summary>
    /// Which dose this was, as the legacy <c>dose_number</c>; null where it is
    /// not recorded.
    /// </summary>
    public int? DoseNumber { get; set; }

    /// <summary>The vaccine's lot number, as the legacy <c>lot_number</c>; null where unrecorded.</summary>
    public string? LotNumber { get; set; }

    /// <summary>
    /// The day the lot expires, as the legacy <c>expiration_date</c>; null
    /// where unrecorded. A <see cref="DateOnly"/> over a legacy <c>date</c>.
    /// </summary>
    public DateOnly? ExpirationDate { get; set; }

    /// <summary>
    /// A free-text note on the record, as the legacy <c>comment</c>; left
    /// unbounded as other free-text columns are.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// When the record was entered. A <see cref="DateTimeOffset"/> as
    /// <see cref="Outbreak.CreatedAt"/> is; set where the record is created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
