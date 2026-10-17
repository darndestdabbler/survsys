namespace Survsys.Data;

/// <summary>
/// A case morbidity report's classification: the one of these a report's case
/// status holds. Unknown is the absence of a classification, which is how an
/// unclassified case is left.
/// </summary>
/// <remarks>
/// The statuses and their codes are EpiTrax's own, as
/// <c>../nedss/docs/business-logic/CLASSIFICATION_RULES.md</c> section 1.2
/// lists them, reduced to the five the session plan names:
/// <see cref="Confirmed"/>, <see cref="Probable"/>, <see cref="Suspected"/>,
/// <see cref="NotACase"/> and <see cref="Unknown"/>. The two legacy codes this
/// installation does not use — <c>D</c> Discarded and <c>OS</c> Out of State —
/// are not here.
/// </remarks>
public enum CaseStatus
{
    /// <summary>
    /// No classification has been made. The legacy leaves an unclassified
    /// case's status empty, so this is the status an empty or absent code
    /// reads as rather than a code of its own.
    /// </summary>
    Unknown,

    /// <summary>Confirmed, the legacy <c>C</c>.</summary>
    Confirmed,

    /// <summary>Probable, the legacy <c>P</c>.</summary>
    Probable,

    /// <summary>Suspected, the legacy <c>S</c>.</summary>
    Suspected,

    /// <summary>Not a Case, the legacy <c>NC</c>.</summary>
    NotACase,
}

/// <summary>
/// The case status vocabulary, and the two rules a status field needs of
/// itself: reading a stored code into one of the five, and whether an offered
/// code is one a report may be set to.
/// </summary>
/// <remarks>
/// The codes are EpiTrax's own, as
/// <c>../nedss/docs/business-logic/CLASSIFICATION_RULES.md</c> section 1.2
/// dumps them on the <c>CaseStatus</c> table: <c>C</c> Confirmed, <c>P</c>
/// Probable, <c>S</c> Suspected and <c>NC</c> Not a Case. The fifth,
/// <see cref="CaseStatus.Unknown"/>, has no code and is the empty or absent
/// value: the legacy leaves an unclassified case's status empty, which section
/// 3.7's override — "blocks auto-classification ... status stays empty" —
/// shows is a state and not a mistake. There is no <c>CaseStatus</c> table in
/// this schema; the codes are a fixed CDC vocabulary rather than an
/// installation-configurable list, so they live here and the status column
/// holds the code against this rule.
///
/// This classifies nothing. The disease-specific validators that read a
/// report's labs, address and exposures to decide a status are PostgreSQL
/// functions in the legacy and Utah-specific, and section 8 defers them to a
/// later service-layer, configurable rules engine; what is here is the
/// vocabulary such a rule would assign from and the reading of a code, not the
/// deciding of one.
///
/// Pure, and public and static in the shape <c>Survsys.Api</c>'s record number
/// rule has, because the unit suite proves it without starting a host. It
/// takes no <c>DbContext</c>: a status code is a string a caller has in hand,
/// and what is here is the reading of it.
/// </remarks>
public static class CaseStatusRules
{
    /// <summary>The code <see cref="CaseStatus.Confirmed"/> is stored under.</summary>
    public const string ConfirmedCode = "C";

    /// <summary>The code <see cref="CaseStatus.Probable"/> is stored under.</summary>
    public const string ProbableCode = "P";

    /// <summary>The code <see cref="CaseStatus.Suspected"/> is stored under.</summary>
    public const string SuspectedCode = "S";

    /// <summary>The code <see cref="CaseStatus.NotACase"/> is stored under.</summary>
    public const string NotACaseCode = "NC";

    /// <summary>
    /// The status a stored code names.
    /// </summary>
    /// <remarks>
    /// An empty, whitespace or absent code is <see cref="CaseStatus.Unknown"/>,
    /// which is how the legacy leaves an unclassified case; surrounding
    /// whitespace is trimmed before a non-empty code is matched, and the match
    /// is exact — the codes are legacy codes and not display text, so
    /// <c>c</c> is not <c>C</c>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The code is neither empty nor one of the four this vocabulary knows. A
    /// stored value like that was written by something that did not go through
    /// this rule — the shape <c>Survsys.Api</c>'s record number rule refuses a
    /// stored value it cannot read, and here for the same reason.
    /// </exception>
    public static CaseStatus Read(string? storedCode)
    {
        if (Match(storedCode) is { } status)
        {
            return status;
        }

        throw new InvalidOperationException(
            $"'{storedCode}' is not a case status code this tier knows. A stored case "
            + $"status is one of {ConfirmedCode}, {ProbableCode}, {SuspectedCode} or "
            + $"{NotACaseCode}, or empty for an unclassified case read as Unknown. A row "
            + "holding anything else was written by something that did not go through this "
            + "vocabulary.");
    }

    /// <summary>
    /// Whether an offered code is one a report's status may be set to — one of
    /// the four codes, or empty to clear the status to
    /// <see cref="CaseStatus.Unknown"/>.
    /// </summary>
    /// <remarks>
    /// The validity rule the status endpoint turns into a 400: an offered code
    /// this returns false for is refused rather than written, where
    /// <see cref="Read"/> throws because a value it cannot read is already on
    /// file. Null rather than an exception is the right answer here because
    /// "may a case be set to this?" is a question this rule answers about any
    /// string a caller offers.
    /// </remarks>
    public static bool IsAssignable(string? offeredCode) => Match(offeredCode) is not null;

    /// <summary>
    /// The status a code names, or null where a non-empty code is not one of
    /// the four. The common core of <see cref="Read"/> and
    /// <see cref="IsAssignable"/>, which differ only in what they do with a
    /// code the vocabulary does not know.
    /// </summary>
    private static CaseStatus? Match(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return CaseStatus.Unknown;
        }

        return code.Trim() switch
        {
            ConfirmedCode => CaseStatus.Confirmed,
            ProbableCode => CaseStatus.Probable,
            SuspectedCode => CaseStatus.Suspected,
            NotACaseCode => CaseStatus.NotACase,
            _ => null,
        };
    }
}
