namespace Survsys.Data;

/// <summary>
/// One thing validation found wrong with an <see cref="EcrDocument"/>,
/// against the CDA schema or the HL7 schematron, stored with the document it
/// was found in. A finding never stops a document from being taken in.
/// </summary>
public class EcrValidationFinding
{
    public int Id { get; set; }

    /// <summary>The document the finding was made on.</summary>
    public int EcrDocumentId { get; set; }

    public EcrDocument? EcrDocument { get; set; }

    /// <summary>"schema" or "schematron".</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>"fatal", "error" or "warning".</summary>
    public string Severity { get; set; } = string.Empty;

    /// <summary>What is wrong, as the schema or schematron words it.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Where: a line and position, or an XPath; null where neither was given.</summary>
    public string? Location { get; set; }
}
