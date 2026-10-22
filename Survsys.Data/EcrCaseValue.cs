namespace Survsys.Data;

/// <summary>
/// One value or row an eICR version wrote on a case, as it wrote it.
/// </summary>
/// <remarks>
/// <see cref="Target"/> and <see cref="TargetId"/> name the row. For a value
/// on a row the eICR did not create, such as the case's occupation,
/// <see cref="Key"/> is the field's name; for a row it created, such as an
/// encounter, it is the row's natural key, by which a later version finds its
/// counterpart. <see cref="Written"/> is what was written, as text: where the
/// row's current value no longer reads the same, a user has edited it, and a
/// later version leaves it alone.
/// </remarks>
public class EcrCaseValue
{
    public int Id { get; set; }

    /// <summary>The link of the eICR version and case the value was written under.</summary>
    public int EcrCaseLinkId { get; set; }

    public EcrCaseLink? EcrCaseLink { get; set; }

    /// <summary>The table written, such as <c>encounters</c>.</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>The id of the row written.</summary>
    public int TargetId { get; set; }

    /// <summary>The field's name, or the natural key of a row the eICR created.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The value or row as written, as text.</summary>
    public string? Written { get; set; }
}
