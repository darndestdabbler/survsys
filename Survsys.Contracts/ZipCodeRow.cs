namespace Survsys.Contracts;

/// <summary>
/// One zip code and the county it lies in: what fills an address's county
/// when it is saved without one and its town gives none.
/// </summary>
/// <param name="Code">The five-digit zip code, its key.</param>
/// <param name="County">The county's code on the county list, or null where none is known.</param>
public record ZipCodeRow(string Code, string? County);
