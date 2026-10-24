namespace Survsys.Contracts;

/// <summary>
/// One condition code a reportability response can carry, an RCTC condition
/// grouper or a SNOMED condition code, and the catalogue condition it maps to.
/// </summary>
/// <param name="Id">The mapping's identifier.</param>
/// <param name="CodeSystem">The code system's OID.</param>
/// <param name="Code">The condition code.</param>
/// <param name="DisplayName">The code's name, or null.</param>
/// <param name="ConditionId">The catalogue condition the code maps to, or null while unmapped.</param>
/// <param name="ConditionName">That condition's name, or null while unmapped.</param>
public record EcrConditionMappingResponse(
    int Id,
    string CodeSystem,
    string Code,
    string? DisplayName,
    int? ConditionId,
    string? ConditionName);
