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
}
