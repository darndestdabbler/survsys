namespace Survsys.Contracts;

/// <summary>
/// One agency granted sight of an outbreak: the agency, named, as the
/// outbreak's access list shows it.
/// </summary>
/// <remarks>
/// The grant carries the agency's id and name and no more: the legacy
/// <c>outbreak_access.type</c> is not on the wire, because this release grants
/// sight and does not distinguish the kinds of grant the legacy int allows.
/// </remarks>
/// <param name="AgencyId">The agency granted sight of the outbreak.</param>
/// <param name="AgencyName">The agency's name, as the access list shows it.</param>
public record OutbreakAccessResponse(int AgencyId, string AgencyName);
