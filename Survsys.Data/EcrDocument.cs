namespace Survsys.Data;

/// <summary>
/// One eCR document taken in: an eICR or the reportability response (RR) made
/// for one, kept as the XML it arrived as.
/// </summary>
/// <remarks>
/// A document is taken once, by its id, so <see cref="DocumentId"/> is
/// unique. An eICR's versions share a <see cref="SetId"/> and are ordered by
/// <see cref="VersionNumber"/>; an RR names the eICR it responds to in
/// <see cref="EicrDocumentId"/>, which is matched against an eICR's
/// <see cref="DocumentId"/> rather than held as a key, so the pair is found
/// whichever of the two arrives first.
/// </remarks>
public class EcrDocument
{
    /// <summary>The kind of an electronic initial case report.</summary>
    public const string EicrKind = "eICR";

    /// <summary>The kind of a reportability response.</summary>
    public const string RrKind = "RR";

    /// <summary>The feed a test document arrived on.</summary>
    public const string TestFeed = "test";

    /// <summary>The feed a production document arrived on.</summary>
    public const string ProductionFeed = "production";

    /// <summary>The case status of an eICR written to its cases.</summary>
    public const string LinkedCaseStatus = "linked";

    /// <summary>The case status of an eICR whose patient a reviewer must match.</summary>
    public const string ReviewCaseStatus = "review";

    public int Id { get; set; }

    /// <summary>The document's id (ClinicalDocument/id), its root and extension joined by a caret.</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary><see cref="EicrKind"/> or <see cref="RrKind"/>.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary><see cref="TestFeed"/> or <see cref="ProductionFeed"/>: the folder the document was taken from.</summary>
    public string Feed { get; set; } = string.Empty;

    /// <summary>The id every version of the document shares (ClinicalDocument/setId), or null where it has none.</summary>
    public string? SetId { get; set; }

    /// <summary>The document's version within its set, or null where it has none.</summary>
    public int? VersionNumber { get; set; }

    /// <summary>For an RR, the id of the eICR it responds to; null for an eICR.</summary>
    public string? EicrDocumentId { get; set; }

    /// <summary>The document as it arrived, unchanged.</summary>
    public string RawXml { get; set; } = string.Empty;

    /// <summary>
    /// The document's sender: its custodian's id (else name) as parsed, which
    /// an <see cref="EcrSender"/> is matched by; null where it names none.
    /// </summary>
    public string? Sender { get; set; }

    /// <summary>When the document says it was created (ClinicalDocument/effectiveTime), in UTC, or null.</summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>The name of the file the document was read from.</summary>
    public string? Source { get; set; }

    /// <summary>When the document was taken in, in UTC.</summary>
    public DateTime ReceivedAt { get; set; }

    /// <summary>What validation against the CDA schema and the HL7 schematron found, if anything.</summary>
    public List<EcrValidationFinding> Findings { get; set; } = [];

    /// <summary>For an RR, the conditions kept at intake as this jurisdiction's to act on; empty for an eICR.</summary>
    public List<EcrRrCondition> RrConditions { get; set; } = [];

    /// <summary>
    /// For an eICR, <see cref="LinkedCaseStatus"/> once it is written to its
    /// cases or <see cref="ReviewCaseStatus"/> while a reviewer must match its
    /// patient; null until either is decided, and for an RR.
    /// </summary>
    public string? CaseStatus { get; set; }

    /// <summary>Why the eICR is held for review, or null.</summary>
    public string? CaseReason { get; set; }

    /// <summary>For an eICR, the cases it was written to.</summary>
    public List<EcrCaseLink> CaseLinks { get; set; } = [];
}
