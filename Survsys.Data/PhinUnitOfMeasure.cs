namespace Survsys.Data;

/// <summary>
/// One unit of measure a rule set whose format type is <c>unitOfMeasure</c>
/// resolves through: the code it is looked up by, and the name, HL7 code and
/// code system it is written with.
/// </summary>
/// <remarks>
/// The legacy NMI <c>phin_unit_of_measure</c> table, as described in
/// <c>../nedss/docs/business-logic/NMI_XML_GENERATION.md</c>, <i>Value Set
/// System</i>: a value set's columns without the CDC name. Unique on the value
/// set and concept code. There is no behaviour here.
/// </remarks>
public class PhinUnitOfMeasure
{
    /// <summary>The unit's id. The key.</summary>
    public int Id { get; set; }

    /// <summary>The value set the unit belongs to.</summary>
    public required string ValueSetName { get; set; }

    /// <summary>The concept code a rule set's value is looked up by.</summary>
    public required string ConceptCode { get; set; }

    /// <summary>The unit's preferred name.</summary>
    public required string PreferredConceptName { get; set; }

    /// <summary>The unit's HL7 code, where set.</summary>
    public string? Hl7Code { get; set; }

    /// <summary>The OID of the code system the unit belongs to, where set.</summary>
    public string? CodeSystemOid { get; set; }
}
