namespace Survsys.Contracts;

/// <summary>
/// One identifier of a person, as the Demographic tab sends it to be saved.
/// </summary>
/// <remarks>
/// <see cref="PersonIdentifierResponse"/> without its identifier, for the
/// reason <see cref="PersonAddressRequest"/> is without one.
/// <see cref="Value"/> is nullable for the reason
/// <see cref="UpdatePersonRequest.LastName"/> is: a row sent without one is
/// answered with a 400 naming the field.
/// </remarks>
/// <param name="IdentifierType">What kind of identifier this is, such as <c>MR</c>.</param>
/// <param name="Value">The identifier itself.</param>
/// <param name="AssigningAuthority">The CLIA number or OID of the authority that assigned it.</param>
/// <param name="AssigningAuthorityType"><c>CLIA</c> or <c>OID</c>: what the authority is named by.</param>
public record PersonIdentifierRequest(
    string? IdentifierType = null,
    string? Value = null,
    string? AssigningAuthority = null,
    string? AssigningAuthorityType = null);
