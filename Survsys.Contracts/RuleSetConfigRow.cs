namespace Survsys.Contracts;

/// <summary>
/// A rule set configuration whole — what it reads, its parameters, its value
/// maps and its expressions — as
/// <c>/api/notification-setup/rule-sets</c> reads and replaces it.
/// </summary>
/// <param name="Id">The configuration's id; ignored when one is created.</param>
/// <param name="FormatType">The format its value is produced in (for example <c>string</c> or <c>phinValueSet</c>), or null.</param>
/// <param name="ValueSetName">The value set coded output resolves through, or null.</param>
/// <param name="Repeating">Whether every formatted response is carried, not only the first.</param>
/// <param name="Fields">The fields it reads, in order.</param>
/// <param name="Data">Its named parameters (for example <c>staticValue</c> or <c>dataSet</c>).</param>
/// <param name="ValueMaps">Its value translations, in order.</param>
/// <param name="Expressions">Its conditional expressions.</param>
public record RuleSetConfigRow(
    int Id,
    string? FormatType,
    string? ValueSetName,
    bool Repeating,
    IReadOnlyList<string> Fields,
    IReadOnlyList<RuleSetDataRow> Data,
    IReadOnlyList<RuleSetValueMapRow> ValueMaps,
    IReadOnlyList<RuleSetExpressionRow> Expressions);
