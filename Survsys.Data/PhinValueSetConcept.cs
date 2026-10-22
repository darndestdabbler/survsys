namespace Survsys.Data;

/// <summary>
/// One concept of a CDC PHIN value set: the code a rule set's coded output is
/// looked up by, and the name, HL7 code and code system it is written with.
/// </summary>
/// <remarks>
/// The legacy NMI <c>phin_value_set</c> table, as described in
/// <c>../nedss/docs/business-logic/NMI_XML_GENERATION.md</c>, <i>Value Set
/// System</i>: where <see cref="CdcName"/> is set it is the name written, over
/// <see cref="PreferredConceptName"/>. Unique on the value set and concept
/// code. There is no behaviour here.
/// </remarks>
public class PhinValueSetConcept
{
    /// <summary>The concept's id. The key.</summary>
    public int Id { get; set; }

    /// <summary>The value set the concept belongs to (for example <c>PHVS_YesNoUnknown_CDC</c>).</summary>
    public required string ValueSetName { get; set; }

    /// <summary>The concept code a rule set's value is looked up by.</summary>
    public required string ConceptCode { get; set; }

    /// <summary>The concept's preferred name.</summary>
    public required string PreferredConceptName { get; set; }

    /// <summary>The concept's HL7 table 0396 code, where set.</summary>
    public string? Hl7Code { get; set; }

    /// <summary>The OID of the code system the concept belongs to, where set.</summary>
    public string? CodeSystemOid { get; set; }

    /// <summary>CDC's name for the concept, written over the preferred name where set.</summary>
    public string? CdcName { get; set; }
}
