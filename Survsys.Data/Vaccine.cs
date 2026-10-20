namespace Survsys.Data;

/// <summary>
/// A vaccine: a definition in the reference catalogue a case's vaccine records
/// (<see cref="PersonVaccine"/>) are chosen from, identified by its CDC code.
/// </summary>
/// <remarks>
/// The columns are EpiTrax's own <c>vaccine</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0169__create_vaccine_tables.sql</c>
/// under <c>../nedss</c>: a CDC code, a description and a sort order. The
/// catalogue is seeded from the CDC vaccine codes that file inserts, so it is
/// reference data an operator chooses from rather than one they maintain.
///
/// <see cref="Code"/> is required and unique where the legacy column is
/// <c>text NOT NULL UNIQUE</c> — a record names a vaccine by its code. There is
/// no navigation out; a record points at a vaccine through
/// <see cref="PersonVaccine.VaccineId"/>.
/// </remarks>
public class Vaccine
{
    public int Id { get; set; }

    /// <summary>The vaccine's CDC code, as the legacy <c>code</c>; unique.</summary>
    public required string Code { get; set; }

    /// <summary>
    /// The vaccine's name, as the legacy <c>description</c> — the text a picker
    /// shows it by; null where it has none.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Where the vaccine falls in the catalogue's order, as the legacy
    /// <c>sort_order</c>.
    /// </summary>
    public int SortOrder { get; set; }
}
