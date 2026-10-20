namespace Survsys.Data;

/// <summary>
/// A treatment: a named drug or regimen the reference catalogue offers, which a
/// condition lists through <see cref="ConditionTreatment"/> so an investigator
/// is shown the treatments relevant to the case's disease.
/// </summary>
/// <remarks>
/// The columns are EpiTrax's own <c>treatment</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, down to their snake_case names, kept to the lean set
/// a catalogue entry needs: its name, whether it is active and whether it is a
/// default. The legacy <c>treatment_type_id</c> is left out with its
/// <c>treatment_type</c> table — a treatment this release is its name and its
/// two flags and no classification behind it.
///
/// <see cref="Name"/> is required where the legacy <c>treatment_name</c> is
/// <c>NOT NULL</c>, as <see cref="Condition.Name"/> is: a treatment with no
/// name cannot be listed or chosen. <see cref="Active"/> and
/// <see cref="DefaultTreatment"/> are non-nullable bools where the legacy
/// columns are <c>NOT NULL</c> with defaults. The conditions a treatment is
/// listed for are the join rows in <see cref="ConditionTreatment"/>; there is
/// no navigation out.
/// </remarks>
public class Treatment
{
    public int Id { get; set; }

    /// <summary>
    /// The treatment's name, as the legacy <c>treatment_name</c> — the text a
    /// list and a picker show it by.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Whether the treatment may be listed for a condition and chosen, as the
    /// legacy <c>active</c>; true by default.
    /// </summary>
    public bool Active { get; set; } = true;

    /// <summary>
    /// Whether the treatment is a default for the conditions it is listed for,
    /// as the legacy <c>default_treatment</c>; false by default.
    /// </summary>
    public bool DefaultTreatment { get; set; }

    /// <summary>
    /// When the treatment was added to the catalogue. A
    /// <see cref="DateTimeOffset"/> as <see cref="Outbreak.CreatedAt"/> is;
    /// set where the treatment is created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
