namespace Survsys.Data;

/// <summary>
/// A master concept of the generic vocabulary — a specimen source or a test
/// result — and the application value Survsys records for it: the master and
/// application tiers a lab's own code is translated through.
/// </summary>
/// <remarks>
/// The legacy <c>vocab_master_vocab</c> and <c>vocab_master2app</c> tables, as
/// described in <c>../nedss/docs/business-logic/EMSA_VOCABULARY_SYSTEM.md</c>,
/// folded into one row because Survsys is the one application: the legacy kept
/// a translation per registered application, and there is only this one. The
/// category is a short code (<c>specimen</c> or <c>test_result</c>) rather
/// than an id into a category table, as the doc's port notes recommend. A
/// lab's code reaches this row through <see cref="VocabChildValue"/>. There is
/// no behaviour here and no navigation out.
/// </remarks>
public class VocabMasterValue
{
    public int Id { get; set; }

    /// <summary>The category this concept belongs to: <c>specimen</c> or <c>test_result</c>.</summary>
    public required string Category { get; set; }

    /// <summary>The master concept, as the vocabulary names it (for example <c>Detected</c>).</summary>
    public required string Concept { get; set; }

    /// <summary>
    /// The value Survsys records for the concept — a result's interpretation
    /// (for example <c>Positive</c>) or a specimen source — where it has one.
    /// The legacy <c>vocab_master2app.coded_value</c>.
    /// </summary>
    public string? AppValue { get; set; }
}
