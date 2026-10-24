namespace Survsys.Data;

/// <summary>
/// Another name the same person is known by — a maiden name, a nickname, the
/// spelling a reporting laboratory used.
/// </summary>
/// <remarks>
/// EpiTrax's own <c>person_alias</c> table, keyed on the person and the alias
/// together with no surrogate key, exactly as <see cref="PersonRace"/> is.
///
/// The text property is <see cref="Alias"/> over the <c>person_alias</c>
/// column: C# will not let a property share the name of the type it is
/// declared on, so the column name and the property name part here, and this
/// is the one place in this entity where they do.
/// </remarks>
public class PersonAlias
{
    /// <summary>The person known by this name.</summary>
    public int PersonId { get; set; }

    /// <summary>The other name itself, as it was written.</summary>
    public required string Alias { get; set; }
}
