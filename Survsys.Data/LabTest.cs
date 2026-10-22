namespace Survsys.Data;

/// <summary>
/// A test run on a laboratory specimen: the middle of the chain from a
/// specimen to its results.
/// </summary>
/// <remarks>
/// The columns are EpiTrax's own <c>lab_test</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, down to their snake_case names.
///
/// A test is a detail of the specimen it is run on, so <see cref="LabId"/> is
/// required and a test goes when its specimen does. The test type and the
/// status are bare nullable ids into reference tables that do not exist here
/// yet, exactly as the specimen's facility and source are; the LOINC code is
/// the lab's own code for the test, held as a plain string.
///
/// There is no behaviour here and no navigation out: the results that hang off
/// a test are read by composing their own query.
/// </remarks>
public class LabTest
{
    public int Id { get; set; }

    /// <summary>The specimen this test was run on.</summary>
    public int LabId { get; set; }

    /// <summary>
    /// What test this is, as the legacy <c>test_type_id</c>. A bare nullable id
    /// into the <c>lab_test_type</c> reference table, which does not exist here
    /// yet.
    /// </summary>
    public int? TestTypeId { get; set; }

    /// <summary>
    /// The status of the test, as the legacy <c>test_status_id</c>. A bare
    /// nullable id into the <c>lab_test_status</c> reference table, which does
    /// not exist here yet.
    /// </summary>
    public int? TestStatusId { get; set; }

    /// <summary>The lab's LOINC code for the test, where it has one.</summary>
    public string? LoincCode { get; set; }

    /// <summary>
    /// The reference range the result is read against, as free text where the
    /// lab reports one.
    /// </summary>
    public string? ReferenceRange { get; set; }

    /// <summary>
    /// The date the test was run, where it is known.
    /// </summary>
    /// <remarks>
    /// A <see cref="DateOnly"/> where the legacy column is a <c>TIMESTAMP</c>,
    /// for the reason <see cref="Lab.CollectionDate"/> is one.
    /// </remarks>
    public DateOnly? LabTestDate { get; set; }
}
