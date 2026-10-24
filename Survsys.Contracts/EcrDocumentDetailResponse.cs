namespace Survsys.Contracts;

/// <summary>
/// One eCR document as the document screen shows it: the eICR rendered
/// through the HL7 CDA stylesheet, beside its reportability responses'
/// conditions and the validation findings of each document.
/// </summary>
/// <remarks>
/// An eICR and its RRs are read together whichever of them is asked for. For
/// an eICR, the conditions are those of every RR that responds to it. For an
/// RR, they are its own, and the HTML is the eICR it responds to, where that
/// eICR is on file.
/// </remarks>
/// <param name="Document">The document's queue summary.</param>
/// <param name="EicrHtml">
/// The eICR rendered as HTML, or null for an RR whose eICR has not arrived.
/// The HTML is the document's own text, so it is shown sandboxed.
/// </param>
/// <param name="Conditions">The RR conditions, with their determinations and responsible agencies.</param>
/// <param name="Findings">The validation findings of the eICR and its RRs, the eICR's first.</param>
/// <param name="CaseStatus">
/// The eICR's case status: <c>linked</c> once it is written to its cases,
/// <c>review</c> while a reviewer must match its patient, or null until
/// either is decided or where the eICR is not on file.
/// </param>
/// <param name="CaseReason">Why the eICR is held for review, or null.</param>
/// <param name="CaseIds">The cases the eICR was written to.</param>
public record EcrDocumentDetailResponse(
    EcrDocumentResponse Document,
    string? EicrHtml,
    IReadOnlyList<EcrConditionResponse> Conditions,
    IReadOnlyList<EcrValidationFindingResponse> Findings,
    string? CaseStatus = null,
    string? CaseReason = null,
    IReadOnlyList<int>? CaseIds = null);
