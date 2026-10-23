namespace Survsys.Contracts;

/// <summary>
/// One thing validation found wrong with an eCR document, against the CDA
/// schema or the HL7 schematron.
/// </summary>
/// <param name="DocumentKind">"eICR" or "RR": the kind of document the finding was made on.</param>
/// <param name="DocumentId">The id of the document the finding was made on.</param>
/// <param name="Source">"schema" or "schematron".</param>
/// <param name="Severity">"fatal", "error" or "warning".</param>
/// <param name="Message">What is wrong.</param>
/// <param name="Location">Where: a line and position, or an XPath; null where neither was given.</param>
public record EcrValidationFindingResponse(
    string DocumentKind,
    string DocumentId,
    string Source,
    string Severity,
    string Message,
    string? Location);
