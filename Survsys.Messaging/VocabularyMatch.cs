namespace Survsys.Messaging;

/// <summary>
/// What <see cref="VocabularyMatcher"/> makes of a code: the condition and
/// organism it resolved to, and whether anything matched at all. An unmatched
/// code yields <see cref="Unmatched"/> rather than an exception, because a code
/// the vocabulary does not carry is an ordinary outcome of real lab traffic,
/// not a fault.
/// </summary>
/// <param name="Matched">Whether the code resolved to a vocabulary entry.</param>
/// <param name="ConditionId">The resolved condition, where the entry names one.</param>
/// <param name="OrganismId">The resolved organism, where the entry names one.</param>
public sealed record VocabularyMatch(bool Matched, int? ConditionId, int? OrganismId)
{
    /// <summary>The result for a code the vocabulary does not carry.</summary>
    public static VocabularyMatch Unmatched { get; } = new(false, null, null);
}
