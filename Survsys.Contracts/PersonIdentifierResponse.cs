namespace Survsys.Contracts;

/// <summary>
/// One identifier another system knows a person by, with the authority that
/// assigned it.
/// </summary>
/// <param name="Id">The identifier row's own identifier.</param>
/// <param name="IdentifierType">What kind of identifier this is, such as <c>MR</c>.</param>
/// <param name="Value">The identifier itself.</param>
/// <param name="AssigningAuthority">The CLIA number or OID of the authority that assigned it.</param>
/// <param name="AssigningAuthorityType"><c>CLIA</c> or <c>OID</c>: what the authority is named by.</param>
public record PersonIdentifierResponse(
    int Id,
    string? IdentifierType,
    string Value,
    string? AssigningAuthority,
    string? AssigningAuthorityType);
