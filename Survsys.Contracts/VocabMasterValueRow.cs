namespace Survsys.Contracts;

/// <summary>
/// A master specimen or test-result concept and the value Survsys records for
/// it, as <c>/api/vocabulary/master-values</c> lists, saves and carries in CSV.
/// </summary>
/// <param name="Id">The entry's identifier; ignored when one is created.</param>
/// <param name="Category">The category: <c>specimen</c> or <c>test_result</c>.</param>
/// <param name="Concept">The master concept.</param>
/// <param name="AppValue">The value Survsys records — a result's interpretation, or a specimen — or null.</param>
public record VocabMasterValueRow(
    int Id,
    string Category,
    string Concept,
    string? AppValue);
