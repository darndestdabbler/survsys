namespace Survsys.Data;

/// <summary>
/// The result of a test run on a laboratory specimen: the end of the chain
/// from a specimen to its tests to their results.
/// </summary>
/// <remarks>
/// The columns are EpiTrax's own <c>lab_test_result</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, down to their snake_case names.
///
/// A result is a detail of the test it belongs to, so <see cref="LabTestId"/>
/// is required and a result goes when its test does. The organism a result
/// names is a bare nullable id into the <c>organism</c> reference table, which
/// does not exist here yet; the result value, the coded result, the units and
/// the comment are the free-text the lab reports.
///
/// There is no behaviour here and no navigation out.
/// </remarks>
public class LabTestResult
{
    public int Id { get; set; }

    /// <summary>The test this is a result of.</summary>
    public int LabTestId { get; set; }

    /// <summary>
    /// The organism the result identifies, as the legacy <c>organism_id</c>. A
    /// bare nullable id into the <c>organism</c> reference table, which does
    /// not exist here yet.
    /// </summary>
    public int? OrganismId { get; set; }

    /// <summary>The measured value, as the lab reports it.</summary>
    public string? ResultValue { get; set; }

    /// <summary>
    /// The coded result — Positive/Reactive, Negative/Non-reactive and the
    /// rest — as free text where the lab reports one.
    /// </summary>
    public string? TestResult { get; set; }

    /// <summary>The lab's free-text comment on the result, where it has one.</summary>
    public string? Comment { get; set; }

    /// <summary>The units the value is in, where it has any.</summary>
    public string? Units { get; set; }

    /// <summary>
    /// Whether this is the primary result of its test — the one a summary
    /// shows. The legacy column defaults to false.
    /// </summary>
    public bool PrimaryLabResult { get; set; }
}
