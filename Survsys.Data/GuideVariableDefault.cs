namespace Survsys.Data;

/// <summary>
/// A notification header variable and the value every guide uses unless it
/// sets its own: the sending and receiving application and facility, and the
/// HL7 version.
/// </summary>
/// <remarks>
/// The legacy NMI <c>guide_variable_default</c> table, as described in
/// <c>../nedss/docs/business-logic/NMI_XML_GENERATION.md</c>, <i>Header
/// Variables</i>. An application or facility is held as its HD components
/// joined by <c>^</c> — namespace, universal id, universal id type — which is
/// how MSH-3 through MSH-6 carry it. A guide's own value is
/// <see cref="GuideVariableValue"/>. There is no behaviour here.
/// </remarks>
public class GuideVariableDefault
{
    public const string SendingApp = "sending_app";
    public const string SendingFacility = "sending_facility";
    public const string ReceivingApp = "receiving_app";
    public const string ReceivingFacility = "receiving_facility";
    public const string Hl7Version = "hl7_version";

    /// <summary>The variable's id. The key.</summary>
    public int Id { get; set; }

    /// <summary>The variable's name (for example <c>sending_app</c>), unique.</summary>
    public required string Name { get; set; }

    /// <summary>The value a guide that sets none uses, where set.</summary>
    public string? DefaultValue { get; set; }
}
