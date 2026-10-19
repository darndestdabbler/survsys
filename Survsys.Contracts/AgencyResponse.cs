namespace Survsys.Contracts;

/// <summary>
/// A public health agency, as the Investigating Agency dropdown shows it.
/// </summary>
/// <remarks>
/// An identifier and a name, which is the whole of what choosing an agency
/// needs: the name is what a user reads and the identifier is what a case
/// morbidity report stores. An agency has more to it in the legacy schema — a
/// short name, a jurisdiction, the zip codes that map to it, whether it is
/// still active — and none of that is on the wire until a screen asks for it.
/// </remarks>
/// <param name="Id">
/// The agency's identifier, as a case morbidity report refers to it.
/// </param>
/// <param name="Name">The agency's name, as a dropdown shows it.</param>
public record AgencyResponse(int Id, string Name);
