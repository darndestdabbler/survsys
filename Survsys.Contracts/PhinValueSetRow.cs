namespace Survsys.Contracts;

/// <summary>
/// One concept of a PHIN value set, keyed by its value set and concept code,
/// as <c>/api/notification-setup/phin-value-sets</c> lists, saves and carries
/// in CSV.
/// </summary>
/// <param name="Id">The concept's identifier; ignored when one is created.</param>
/// <param name="ValueSetName">The value set the concept belongs to.</param>
/// <param name="ConceptCode">The code a rule set's value is looked up by.</param>
/// <param name="PreferredConceptName">The concept's preferred name.</param>
/// <param name="Hl7Code">The concept's HL7 code, or null.</param>
/// <param name="CodeSystemOid">The OID of the concept's code system, or null.</param>
/// <param name="CdcName">CDC's name for the concept, written over the preferred name; or null.</param>
public record PhinValueSetRow(
    int Id,
    string ValueSetName,
    string ConceptCode,
    string PreferredConceptName,
    string? Hl7Code,
    string? CodeSystemOid,
    string? CdcName);
