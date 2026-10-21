namespace Survsys.Data;

/// <summary>
/// A name a person was on file under before it was changed: kept so that a
/// search by the old name still finds them.
/// </summary>
/// <remarks>
/// Written by a save that changes the person's last, first or middle name, and
/// never edited after: it is a record of what the name was, not another name
/// the person goes by — that is a <see cref="PersonAlias"/>.
/// </remarks>
public class PersonNameHistory
{
    public int Id { get; set; }

    /// <summary>The person who was on file under this name.</summary>
    public int PersonId { get; set; }

    /// <summary>The last name as it was.</summary>
    public required string LastName { get; set; }

    /// <summary>The first name as it was.</summary>
    public string? FirstName { get; set; }

    /// <summary>The middle name as it was.</summary>
    public string? MiddleName { get; set; }

    /// <summary>The day the name was changed from this one.</summary>
    public DateOnly ChangedOn { get; set; }
}
