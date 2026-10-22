namespace Survsys.Data;

/// <summary>
/// One race a person reported. A person reports as many as apply, which is
/// what the Demographic tab's multi-select means.
/// </summary>
/// <remarks>
/// EpiTrax's own <c>person_race</c> table, and it is keyed the way that table
/// is: on the person and the race together, with no surrogate key. The key is
/// the whole row, so the schema itself refuses the same race twice for one
/// person — which is exactly what a multi-select promises, and what an invented
/// identity column would have left to the code that writes rows.
/// </remarks>
public class PersonRace
{
    /// <summary>The person who reported this race.</summary>
    public int PersonId { get; set; }

    /// <summary>The race, as the legacy race code.</summary>
    public required string Race { get; set; }
}
