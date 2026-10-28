namespace Survsys.Data;

/// <summary>
/// A test type a hepatitis lab group carries: the test group it is reported
/// under and where it sorts.
/// </summary>
/// <remarks>
/// The legacy <c>hep_lab_group_test_type</c> and
/// <c>hep_lab_group_test_type_order</c> tables, held as one row. Keyed by the
/// group and the test type; <see cref="TestTypeId"/> is a bare id into the
/// <c>test_type</c> code values. There is no behaviour here and no navigation
/// out.
/// </remarks>
public class HepLabGroupTestType
{
    /// <summary>The group, as a bare id into <c>nmi_hep_lab_group</c>.</summary>
    public int HepLabGroupId { get; set; }

    /// <summary>The test type, as a bare id into the <c>test_type</c> code values.</summary>
    public int TestTypeId { get; set; }

    /// <summary>The test group the test type is reported under (for example <c>HBsAg</c>).</summary>
    public required string TestGroupName { get; set; }

    /// <summary>Where the test type sorts in the group, ascending.</summary>
    public int SortOrder { get; set; }
}
