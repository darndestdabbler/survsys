namespace Survsys.Data;

/// <summary>
/// One field of a case morbidity report that core element configuration can
/// show or hide.
/// </summary>
/// <remarks>
/// EpiTrax's own <c>core_element</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, cut to what visibility needs: the stable
/// <see cref="Uid"/> a screen names the field by, and its label. The legacy's
/// three data set levels (minimal, optimal, hidden) are cut to one flag,
/// <see cref="Hidden"/>, because whether a field is shown is the whole of what
/// this configuration decides here.
///
/// <see cref="Tab"/> is not a legacy column: the legacy groups its elements
/// under <c>core_section</c>, which this schema does not carry, and the tab is
/// what the configuration screen groups them by instead.
/// </remarks>
public class CoreElement
{
    public int Id { get; set; }

    /// <summary>
    /// The identifier a report tab names the field by, such as
    /// <c>disease_onset_date</c>. Unique.
    /// </summary>
    public required string Uid { get; set; }

    /// <summary>The field's label, as the tab shows it.</summary>
    public required string Label { get; set; }

    /// <summary>The report tab the field is on.</summary>
    public required string Tab { get; set; }

    /// <summary>Whether the field is hidden where no override says otherwise.</summary>
    public bool Hidden { get; set; }
}
