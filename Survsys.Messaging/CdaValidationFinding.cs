namespace Survsys.Messaging;

/// <summary>
/// One thing <see cref="CdaValidator"/> found wrong with an eCR document.
/// </summary>
/// <param name="Source"><see cref="CdaValidator.SchemaSource"/> or <see cref="CdaValidator.SchematronSource"/>.</param>
/// <param name="Severity"><see cref="CdaValidator.Fatal"/>, <see cref="CdaValidator.Error"/> or <see cref="CdaValidator.Warning"/>.</param>
/// <param name="Message">What is wrong, as the schema or the schematron words it.</param>
/// <param name="Location">Where: a line and position for a schema finding, an XPath for a schematron finding.</param>
public record CdaValidationFinding(string Source, string Severity, string Message, string? Location);
