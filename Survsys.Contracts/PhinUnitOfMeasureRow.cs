namespace Survsys.Contracts;

/// <summary>
/// One unit of measure, keyed by its value set and concept code, as
/// <c>/api/notification-setup/units-of-measure</c> lists, saves and carries in
/// CSV.
/// </summary>
/// <param name="Id">The unit's identifier; ignored when one is created.</param>
/// <param name="ValueSetName">The value set the unit belongs to.</param>
/// <param name="ConceptCode">The code a rule set's value is looked up by.</param>
/// <param name="PreferredConceptName">The unit's preferred name.</param>
/// <param name="Hl7Code">The unit's HL7 code, or null.</param>
/// <param name="CodeSystemOid">The OID of the unit's code system, or null.</param>
public record PhinUnitOfMeasureRow(
    int Id,
    string ValueSetName,
    string ConceptCode,
    string PreferredConceptName,
    string? Hl7Code,
    string? CodeSystemOid);
