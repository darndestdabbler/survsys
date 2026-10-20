namespace Survsys.Contracts;

/// <summary>
/// A vaccine to add to the catalogue or to save over one: what
/// <c>POST /api/vaccines</c> and <c>PUT /api/vaccines/{id}</c> take, and one
/// row of the catalogue's CSV file.
/// </summary>
/// <remarks>
/// A vaccine is retired by clearing <paramref name="Active"/>, never deleted,
/// so a case's record that names it still shows it. A blank description is
/// stored as null.
/// </remarks>
/// <param name="Code">The vaccine's CDC code, unique in the catalogue. Required.</param>
/// <param name="Description">The vaccine's name, or null.</param>
/// <param name="SortOrder">Where the vaccine falls in the catalogue's order; zero or more.</param>
/// <param name="Active">Whether the vaccine may be chosen for a new record. Active by default.</param>
public record SaveVaccineRequest(
    string Code,
    string? Description = null,
    int SortOrder = 0,
    bool Active = true);
