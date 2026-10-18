namespace Survsys.Messaging;

/// <summary>
/// Matches the codes a lab message carries against the <see cref="Vocabulary"/>:
/// a LOINC to its condition and organism, and a SNOMED to its organism and
/// condition. This is the vocabulary half of what Mirth did in the legacy
/// pipeline, over data rather than a database — the matcher is built once over
/// a vocabulary and holds it as lookups, and reaches for no DbContext.
/// </summary>
/// <remarks>
/// The vocabulary's keys are unique — the Survsys.Data tables carry a unique
/// index on each (the LOINC, the SNOMED, and the lab-and-child-code pair) — so
/// the lookups are built by that key and resolve one entry each. A code the
/// vocabulary does not carry yields <see cref="VocabularyMatch.Unmatched"/>
/// rather than an exception.
/// </remarks>
public sealed class VocabularyMatcher
{
    private readonly IReadOnlyDictionary<string, MasterLoincEntry> _masterByLoinc;
    private readonly IReadOnlyDictionary<(int LabId, string ChildLoinc), ChildLoincEntry> _childByLabCode;
    private readonly IReadOnlyDictionary<string, MasterOrganismEntry> _masterBySnomed;

    public VocabularyMatcher(Vocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);

        _masterByLoinc = vocabulary.MasterLoincs.ToDictionary(
            entry => entry.Loinc, StringComparer.Ordinal);
        _childByLabCode = vocabulary.ChildLoincs.ToDictionary(
            entry => (entry.LabId, entry.ChildLoinc));
        _masterBySnomed = vocabulary.MasterOrganisms.ToDictionary(
            entry => entry.Snomed, StringComparer.Ordinal);
    }

    /// <summary>
    /// Match a LOINC code reported by a given lab. A lab-specific child-LOINC
    /// mapping for that lab and code takes precedence: when one exists, the
    /// code resolves through it to its master LOINC rather than by reading the
    /// code as a master LOINC directly. Either way the result carries the
    /// master's condition and organism. A code with no child mapping for the
    /// lab and no master of its own, or a child mapping whose master the
    /// vocabulary does not carry, is unmatched.
    /// </summary>
    public VocabularyMatch MatchLoinc(string? loinc, int labId)
    {
        if (string.IsNullOrWhiteSpace(loinc))
        {
            return VocabularyMatch.Unmatched;
        }

        var masterCode = _childByLabCode.TryGetValue((labId, loinc), out var child)
            ? child.MasterLoinc
            : loinc;

        return _masterByLoinc.TryGetValue(masterCode, out var master)
            ? new VocabularyMatch(true, master.ConditionId, master.OrganismId)
            : VocabularyMatch.Unmatched;
    }

    /// <summary>
    /// Match a SNOMED code to its master organism, carrying the organism and
    /// the condition that organism reports. A code the vocabulary does not
    /// carry is unmatched.
    /// </summary>
    public VocabularyMatch MatchOrganism(string? snomed)
    {
        if (string.IsNullOrWhiteSpace(snomed))
        {
            return VocabularyMatch.Unmatched;
        }

        return _masterBySnomed.TryGetValue(snomed, out var organism)
            ? new VocabularyMatch(true, organism.ConditionId, organism.OrganismId)
            : VocabularyMatch.Unmatched;
    }
}
