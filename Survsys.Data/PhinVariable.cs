namespace Survsys.Data;

/// <summary>
/// A CDC PHIN variable: one data element the national notification message can
/// carry, named by the identifier a guide and a message refer to it by. The
/// catalogue the outbound half is built over holds the 588 of these the legacy
/// NMI database carries.
/// </summary>
/// <remarks>
/// The columns are the legacy NMI <c>phin_var</c> table, as described in
/// <c>../nedss/docs/business-logic/NMI_RULESETS.md</c>, kept to what naming and
/// placing a variable needs: its identifier, its description, the standard
/// identifier and code system it carries, its HL7 data type and priority, and
/// its status and admin note.
///
/// <see cref="RuleSetId"/> and <see cref="RuleSetConfigId"/> are bare nullable
/// ids held for a later session — the ruleset types and their evaluation are
/// Session 044's work, and this session neither populates nor reads them. The
/// variable is reference data the mapping reads, so there is no behaviour here
/// and no navigation out.
/// </remarks>
public class PhinVariable
{
    /// <summary>
    /// The variable's identifier, as a guide and a message name it (for
    /// example <c>INV163</c> or <c>225517006A</c>). The primary key.
    /// </summary>
    public required string PhinVar { get; set; }

    /// <summary>The human-readable name of the variable.</summary>
    public required string Description { get; set; }

    /// <summary>
    /// The LOINC or other standard identifier the variable carries, where it
    /// has one; null where the identifier alone names it.
    /// </summary>
    public string? Identifier { get; set; }

    /// <summary>The legacy single-letter processing priority, where set.</summary>
    public string? Priority { get; set; }

    /// <summary>
    /// The HL7 v2 data type the value is formatted as (for example
    /// <c>CWE</c>, <c>SN</c>, <c>DT</c>), where set.
    /// </summary>
    public string? Hl7DataType { get; set; }

    /// <summary>
    /// The ruleset type that formats this variable's value, as a bare nullable
    /// id for a later session to wire; not populated or read here.
    /// </summary>
    public int? RuleSetId { get; set; }

    /// <summary>
    /// The specific ruleset configuration, as a bare nullable id for a later
    /// session to wire; not populated or read here.
    /// </summary>
    public int? RuleSetConfigId { get; set; }

    /// <summary>The variable's status, where set.</summary>
    public string? Status { get; set; }

    /// <summary>An administrator's note on the variable, where set.</summary>
    public string? Note { get; set; }

    /// <summary>
    /// The code system the variable's coded values come from (for example
    /// <c>SCT</c>, <c>LN</c>, <c>PHINQUESTION</c>), where set.
    /// </summary>
    public string? CodeSystem { get; set; }
}
