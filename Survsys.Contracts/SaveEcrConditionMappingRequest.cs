namespace Survsys.Contracts;

/// <summary>
/// A condition code to add to the eCR condition mapping or to save over one:
/// what <c>POST /api/ecr-condition-mappings</c> and
/// <c>PUT /api/ecr-condition-mappings/{id}</c> take.
/// </summary>
/// <param name="CodeSystem">The code system's OID, such as SNOMED CT's <c>2.16.840.1.113883.6.96</c>. Required.</param>
/// <param name="Code">The condition code, unique within its code system. Required.</param>
/// <param name="DisplayName">The code's name, or null.</param>
/// <param name="ConditionId">The catalogue condition the code maps to, or null to leave it unmapped.</param>
public record SaveEcrConditionMappingRequest(
    string CodeSystem,
    string Code,
    string? DisplayName = null,
    int? ConditionId = null);
