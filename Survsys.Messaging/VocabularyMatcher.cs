namespace Survsys.Messaging;

/// <summary>
/// Matches the codes a lab message carries against the <see cref="Vocabulary"/>:
/// a LOINC to its condition and organism, a SNOMED to its organism and
/// condition, and a lab's specimen or result code to its application value.
/// This is the vocabulary half of what Mirth did in the legacy pipeline, over
/// data rather than a database — the matcher is built once over a vocabulary
/// and holds it as lookups, and reaches for no DbContext.
/// </summary>
/// <remarks>
/// The three tiers of <c>EMSA_VOCABULARY_SYSTEM.md</c>: a lab's child code is
/// read first, then the master it names, then the master's application value.
/// The vocabulary's keys are unique — the Survsys.Data tables carry a unique
/// index on each (the LOINC, the SNOMED, the lab-and-child-code pair, and the
/// lab, category and child code) — so the lookups are built by that key and
/// resolve one entry each. A lab's child code is matched without regard to
/// case, as the legacy's <c>ILIKE</c> matched it. A code the vocabulary does
/// not carry yields <see cref="VocabularyMatch.Unmatched"/> rather than an
/// exception.
/// </remarks>
public sealed class VocabularyMatcher
{
    private readonly IReadOnlyDictionary<string, MasterLoincEntry> _masterByLoinc;
    private readonly IReadOnlyDictionary<(int LabId, string ChildLoinc), ChildLoincEntry> _childByLabCode;
    private readonly IReadOnlyDictionary<string, MasterOrganismEntry> _masterBySnomed;
    private readonly IReadOnlyDictionary<string, MasterOrganismEntry> _masterBySnomedAlt;
    private readonly IReadOnlyDictionary<int, MasterOrganismEntry> _masterByOrganismId;
    private readonly IReadOnlyDictionary<int, MasterValueEntry> _masterValueById;
    private readonly IReadOnlyDictionary<(int LabId, string Category, string ChildCode), ChildValueEntry> _childValueByLabCode;

    public VocabularyMatcher(Vocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);

        _masterByLoinc = vocabulary.MasterLoincs.ToDictionary(
            entry => entry.Loinc, StringComparer.Ordinal);
        _childByLabCode = vocabulary.ChildLoincs.ToDictionary(
            entry => (entry.LabId, entry.ChildLoinc.Trim().ToUpperInvariant()));
        _masterBySnomed = vocabulary.MasterOrganisms.ToDictionary(
            entry => entry.Snomed, StringComparer.Ordinal);

        // The alternate code is an alias with no unique index, so the first
        // organism to claim one keeps it.
        _masterBySnomedAlt = vocabulary.MasterOrganisms
            .Where(entry => !string.IsNullOrWhiteSpace(entry.SnomedAlt))
            .DistinctBy(entry => entry.SnomedAlt)
            .ToDictionary(entry => entry.SnomedAlt!, StringComparer.Ordinal);
        _masterByOrganismId = vocabulary.MasterOrganisms
            .Where(entry => entry.OrganismId is not null)
            .ToDictionary(entry => entry.OrganismId!.Value);
        _masterValueById = vocabulary.MasterValues.ToDictionary(entry => entry.Id);
        _childValueByLabCode = vocabulary.ChildValues.ToDictionary(
            entry => (entry.LabId, entry.Category, entry.ChildCode.ToUpperInvariant()));
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
    public VocabularyMatch MatchLoinc(string? loinc, int labId) =>
        Master(loinc, labId) is { } master
            ? new VocabularyMatch(
                true,
                master.ConditionId,
                master.OrganismId,
                master.Loinc,
                OrganismName(master.OrganismId))
            : VocabularyMatch.Unmatched;

    /// <summary>
    /// Match one result: its LOINC as <see cref="MatchLoinc"/> does and, where
    /// the master LOINC takes its organism from the result, the result's code
    /// to an organism — through the lab's child organism code first, then as a
    /// SNOMED, primary or alternate. The organism's condition is the result's
    /// where it names one, and the master LOINC's otherwise. A LOINC that
    /// resolves but whose result names no organism the vocabulary carries
    /// matches with the master's condition and no organism.
    /// </summary>
    public VocabularyMatch MatchResult(string? loinc, string? resultCode, int labId)
    {
        if (Master(loinc, labId) is not { } master)
        {
            return VocabularyMatch.Unmatched;
        }

        if (!master.OrganismFromResult)
        {
            return MatchLoinc(loinc, labId);
        }

        var organism = Organism(resultCode, labId);
        return new VocabularyMatch(
            true,
            organism?.ConditionId ?? master.ConditionId,
            organism?.OrganismId,
            master.Loinc,
            organism?.OrganismName);
    }

    /// <summary>
    /// Match a SNOMED code — primary or alternate — to its master organism,
    /// carrying the organism and the condition that organism reports. A code
    /// the vocabulary does not carry is unmatched.
    /// </summary>
    public VocabularyMatch MatchOrganism(string? snomed) =>
        Snomed(snomed) is { } organism
            ? new VocabularyMatch(true, organism.ConditionId, organism.OrganismId, OrganismName: organism.OrganismName)
            : VocabularyMatch.Unmatched;

    /// <summary>
    /// The application value a lab's own code for a specimen or a test result
    /// translates to: through the lab's child code in that category to its
    /// master value, and that master's application value. Null where the lab
    /// has no such code, its master is not carried, or the master has no
    /// application value.
    /// </summary>
    public string? MatchValue(string category, string? code, int labId)
    {
        ArgumentNullException.ThrowIfNull(category);

        return Child(category, code, labId) is { } child
            && _masterValueById.TryGetValue(child.MasterId, out var master)
            && master.Category == category
                ? master.AppValue
                : null;
    }

    private MasterLoincEntry? Master(string? loinc, int labId)
    {
        if (string.IsNullOrWhiteSpace(loinc))
        {
            return null;
        }

        var masterCode = _childByLabCode.TryGetValue((labId, loinc.Trim().ToUpperInvariant()), out var child)
            ? child.MasterLoinc
            : loinc.Trim();

        return _masterByLoinc.GetValueOrDefault(masterCode);
    }

    private MasterOrganismEntry? Organism(string? code, int labId) =>
        Child(VocabularyCategory.Organism, code, labId) is { } child
            ? _masterByOrganismId.GetValueOrDefault(child.MasterId)
            : Snomed(code);

    private MasterOrganismEntry? Snomed(string? snomed)
    {
        if (string.IsNullOrWhiteSpace(snomed))
        {
            return null;
        }

        return _masterBySnomed.GetValueOrDefault(snomed) ?? _masterBySnomedAlt.GetValueOrDefault(snomed);
    }

    private ChildValueEntry? Child(string category, string? code, int labId) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : _childValueByLabCode.GetValueOrDefault((labId, category, code.Trim().ToUpperInvariant()));

    private string? OrganismName(int? organismId) =>
        organismId is { } id ? _masterByOrganismId.GetValueOrDefault(id)?.OrganismName : null;
}
