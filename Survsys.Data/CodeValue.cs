namespace Survsys.Data;

/// <summary>
/// One entry on a reference code list: a code a coded field is chosen from,
/// such as a specimen source, a race or a county.
/// </summary>
/// <remarks>
/// The legacy keeps one table per list (<c>specimen_source</c>, <c>race</c>,
/// <c>county</c> and the rest, each a code, a description and a sort order);
/// here they are one table told apart by <see cref="List"/>, so a new list is
/// rows rather than a migration. The seed is <see cref="CodeValueSeed"/>.
///
/// A coded field that holds a string holds <see cref="Code"/>; one that holds
/// an integer holds <see cref="Id"/>. Neither is a foreign key: a value stored
/// before the lists existed is left as it is, on its list or not. A code is
/// retired by clearing <see cref="Active"/> rather than deleted, so a record
/// that names it still does.
/// </remarks>
public class CodeValue
{
    public int Id { get; set; }

    /// <summary>The list the code is on, in snake_case, such as <c>specimen_source</c>.</summary>
    public required string List { get; set; }

    /// <summary>The code itself, as the legacy <c>code</c>; unique within its list.</summary>
    public required string Code { get; set; }

    /// <summary>
    /// The text a picker shows the code by, as the legacy <c>description</c>;
    /// null where the code is its own label.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>Where the code falls in its list's order, as the legacy <c>sort_order</c>.</summary>
    public int SortOrder { get; set; }

    /// <summary>False once the code is retired: kept for the records naming it, no longer offered.</summary>
    public bool Active { get; set; } = true;

    /// <summary>The <see cref="Kind"/> of a county proper.</summary>
    public const string CountyKind = "county";

    /// <summary>
    /// The <see cref="Kind"/> of what the Census Bureau counts in a county's
    /// place, as Connecticut's planning regions since 2022.
    /// </summary>
    public const string CountyEquivalentKind = "county-equivalent";

    /// <summary>On the county list, the two-letter state the county is in; null elsewhere.</summary>
    public string? State { get; set; }

    /// <summary>
    /// On the county list, the county's five-digit FIPS code; null elsewhere,
    /// and on a legacy county code that has none here.
    /// </summary>
    public string? Fips { get; set; }

    /// <summary>
    /// On the county list, <see cref="CountyKind"/> or
    /// <see cref="CountyEquivalentKind"/>; null elsewhere.
    /// </summary>
    public string? Kind { get; set; }
}
