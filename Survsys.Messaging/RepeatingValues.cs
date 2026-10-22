namespace Survsys.Messaging;

/// <summary>
/// The repeating-values switch every formatter applies before it returns: the
/// simple but critical rule that decides whether a PHIN variable appears once
/// or many times in the output.
/// </summary>
/// <remarks>
/// As the legacy NMI <c>repeatingValues</c> step does, described in
/// <c>../nedss/docs/business-logic/NMI_RULESETS.md</c>: when the ruleset's
/// repeating flag is set, every formatted response is kept; otherwise only the
/// first response that is not empty. When nothing is produced, or nothing
/// non-empty when the flag is clear, the result is empty rather than an error.
/// </remarks>
public static class RepeatingValues
{
    /// <summary>
    /// Applies the repeating rule to <paramref name="responses"/>: all of them
    /// when <paramref name="repeating"/> is set, or the first non-empty one
    /// otherwise.
    /// </summary>
    public static IReadOnlyList<RuleSetFormattedData> Apply(
        bool repeating,
        IEnumerable<RuleSetFormattedData> responses)
    {
        ArgumentNullException.ThrowIfNull(responses);

        if (repeating)
        {
            return responses.ToList();
        }

        var first = responses.FirstOrDefault(response => !response.IsEmpty);
        return first is null ? [] : [first];
    }
}
