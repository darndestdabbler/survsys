// The in-memory ruleset model the seven formatters evaluate over: a loaded
// ruleset configuration, the EpiTrax case record it reads field values and
// datasets from, the value-set concepts coded outputs resolve through, and the
// formatted result a formatter returns. None of these is a Survsys.Data entity,
// and this module reaches for no DbContext: a formatter is given its inputs as
// plain data, so the messaging module keeps the fence against the database that
// the architecture test asserts. Projecting the Survsys.Data ruleset tables
// into these records is the caller's to do, in a tier that may see both. The
// shapes follow the legacy NMI ruleset system described in
// ../nedss/docs/business-logic/NMI_RULESETS.md, kept to what the evaluation needs.
namespace Survsys.Messaging;

/// <summary>
/// A loaded ruleset configuration: everything a formatter needs for one PHIN
/// variable's transformation, gathered from the <c>rule_set_config</c> row and
/// its child tables, with the value set it resolves coded values through.
/// </summary>
/// <param name="TypeLabel">The ruleset type (for example <c>staticValue</c> or <c>conditionMap</c>).</param>
/// <param name="FormatType">The format the value is produced in, where set.</param>
/// <param name="Repeating">Whether the variable repeats: every response, or only the first non-empty one.</param>
/// <param name="ValueSetName">The name of the value set coded outputs resolve through, where set.</param>
/// <param name="Fields">The EpiTrax field names the transformation reads.</param>
/// <param name="Data">The configuration's key-value parameters, by name.</param>
/// <param name="ValueMaps">The value translations the transformation applies.</param>
/// <param name="Expressions">The conditional expressions, in the order they are evaluated.</param>
/// <param name="ValueSet">The concepts of <paramref name="ValueSetName"/>, supplied by the caller.</param>
public sealed record LoadedRuleSet(
    string TypeLabel,
    string? FormatType,
    bool Repeating,
    string? ValueSetName,
    IReadOnlyList<string> Fields,
    IReadOnlyDictionary<string, string?> Data,
    IReadOnlyList<ValueMapping> ValueMaps,
    IReadOnlyList<ConditionExpression> Expressions,
    IReadOnlyList<ValueSetConcept> ValueSet);

/// <summary>
/// A value translation of a ruleset: an input value mapped to an output value.
/// The legacy <c>null_value</c> and <c>default_value</c> fallbacks are ordinary
/// mappings whose <see cref="ValueFrom"/> carries those sentinels.
/// </summary>
/// <param name="ValueFrom">The input value this mapping matches, where set.</param>
/// <param name="ValueTo">The output value this mapping produces, where set.</param>
/// <param name="OtherSpecified">The "other, specify" text this mapping carries, where set.</param>
public sealed record ValueMapping(string? ValueFrom, string? ValueTo, string? OtherSpecified);

/// <summary>
/// A conditional expression of a ruleset: the value it contributes when its
/// conditions match, and the conditions that decide the match.
/// </summary>
/// <param name="Value">The value this expression contributes when it matches, where set.</param>
/// <param name="OtherSpecified">The "other, specify" text this expression carries, where set.</param>
/// <param name="ExecutionOrder">The order this expression is evaluated in, ascending.</param>
/// <param name="Conditions">The conditions, parsed into AND/OR groups by their and-or flag.</param>
public sealed record ConditionExpression(
    string? Value,
    string? OtherSpecified,
    int ExecutionOrder,
    IReadOnlyList<ExpressionCondition> Conditions);

/// <summary>
/// A condition within an expression: one comparison that, with its siblings in
/// the same group, decides whether the expression matches.
/// </summary>
/// <param name="Field">The EpiTrax field this condition reads, where set.</param>
/// <param name="Operator">The comparison operator (<c>=</c>, <c>&lt;&gt;</c>, <c>&gt;</c>, <c>&lt;</c>, <c>&gt;=</c>, <c>&lt;=</c>).</param>
/// <param name="Value">The value compared against, a literal or a field name.</param>
/// <param name="AndOr">The flag that groups this condition: <c>OR</c> starts a new group, <c>AND</c> continues the current one.</param>
/// <param name="ValueIsField">When set, <paramref name="Value"/> names another field to compare against rather than a literal.</param>
public sealed record ExpressionCondition(
    string? Field,
    string? Operator,
    string? Value,
    string? AndOr,
    bool ValueIsField);

/// <summary>
/// A concept of a PHIN value set: the coded value a formatted output carries
/// when its format type resolves through a value set.
/// </summary>
/// <param name="PreferredConceptName">The concept's preferred name, the value typically shown.</param>
/// <param name="Hl7Code">The HL7 code for the concept.</param>
/// <param name="ConceptCode">The concept code the value is looked up by.</param>
/// <param name="CodeSystemOid">The OID of the code system the concept belongs to.</param>
public sealed record ValueSetConcept(
    string PreferredConceptName,
    string? Hl7Code,
    string ConceptCode,
    string? CodeSystemOid);

/// <summary>
/// One row of a repeating dataset (a lab, vaccine, treatment or travel record):
/// its field values by name.
/// </summary>
/// <param name="Fields">The row's field values, by field name.</param>
public sealed record DatasetRow(IReadOnlyDictionary<string, string?> Fields)
{
    /// <summary>The row's value for a field, or null where the row has none.</summary>
    public string? Value(string field) =>
        Fields.TryGetValue(field, out var value) ? value : null;
}

/// <summary>
/// The EpiTrax case record a formatter reads: the case's core field values by
/// name, and the named repeating datasets (Lab, Vaccine, Treatment, Travel) as
/// rows of field values.
/// </summary>
/// <param name="Fields">The core field values, by field name; a value may be pipe-delimited.</param>
/// <param name="Datasets">The repeating datasets, by dataset name.</param>
public sealed record EpiTraxRecord(
    IReadOnlyDictionary<string, string?> Fields,
    IReadOnlyDictionary<string, IReadOnlyList<DatasetRow>> Datasets)
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<DatasetRow>> NoDatasets =
        new Dictionary<string, IReadOnlyList<DatasetRow>>();

    /// <summary>A record with core fields and no datasets.</summary>
    public EpiTraxRecord(IReadOnlyDictionary<string, string?> fields)
        : this(fields, NoDatasets)
    {
    }

    /// <summary>The case's value for a field, or null where the case has none.</summary>
    public string? Value(string field) =>
        Fields.TryGetValue(field, out var value) ? value : null;

    /// <summary>
    /// The case's values for a field, splitting a pipe-delimited value into its
    /// parts and dropping empty ones. An empty or missing field yields none.
    /// </summary>
    public IReadOnlyList<string> Values(string field)
    {
        var raw = Value(field);
        return string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>The rows of a named dataset, or none where the case carries none.</summary>
    public IReadOnlyList<DatasetRow> Dataset(string name) =>
        Datasets.TryGetValue(name, out var rows) ? rows : [];
}

/// <summary>The kind of message a formatted result carries.</summary>
public enum RuleSetMessageKind
{
    /// <summary>A failure: the value could not be produced.</summary>
    Error,

    /// <summary>A caution that does not stop a value being produced.</summary>
    Warning,

    /// <summary>An informational note.</summary>
    Info,
}

/// <summary>
/// One formatted result a formatter produces for a PHIN variable: the text
/// value and, for coded values, the code and concept that go with it, plus any
/// message the formatting raised.
/// </summary>
/// <param name="Value">The formatted text value, where produced.</param>
/// <param name="Code">The HL7 code, for a coded value.</param>
/// <param name="Id">The concept code, for a coded value.</param>
/// <param name="CodeSystemOid">The OID of the code system, for a coded value.</param>
/// <param name="OtherSpecified">The "other, specify" text, where set.</param>
/// <param name="Message">A message the formatting raised, where any.</param>
/// <param name="MessageKind">The kind of <paramref name="Message"/>, where any.</param>
public sealed record RuleSetFormattedData(
    string? Value = null,
    string? Code = null,
    string? Id = null,
    string? CodeSystemOid = null,
    string? OtherSpecified = null,
    string? Message = null,
    RuleSetMessageKind? MessageKind = null)
{
    /// <summary>
    /// Whether this result carries nothing: no value, no code and no message.
    /// The repeating-values helper keeps the first result that is not empty.
    /// </summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Value)
        && string.IsNullOrWhiteSpace(Code)
        && Message is null;
}
