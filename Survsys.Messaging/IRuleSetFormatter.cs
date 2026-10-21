namespace Survsys.Messaging;

/// <summary>
/// A ruleset formatter: one of the seven NMI ruleset types, turning a loaded
/// ruleset configuration and an EpiTrax case record into the formatted results
/// a PHIN variable's value is built from. Each type is one implementation, the
/// strategy the evaluation selects by the ruleset's type label.
/// </summary>
/// <remarks>
/// The formatter reads only the in-memory model it is handed, never the
/// database, so the messaging module keeps its fence against Survsys.Data.
/// </remarks>
public interface IRuleSetFormatter
{
    /// <summary>
    /// Formats a PHIN variable's value from the ruleset and the record,
    /// returning every formatted response the ruleset produces. The optional
    /// dataset name is the repeating-group context (for example <c>Lab</c>)
    /// when the formatting is driven for one dataset row rather than the case
    /// as a whole.
    /// </summary>
    IReadOnlyList<RuleSetFormattedData> Format(
        LoadedRuleSet ruleSet,
        EpiTraxRecord record,
        string? dataSet = null);
}
