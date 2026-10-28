namespace Survsys.Messaging;

/// <summary>
/// A test type a hepatitis lab group carries: the test group it is reported
/// under and where it sorts.
/// </summary>
/// <param name="TestTypeId">The test type.</param>
/// <param name="TestGroupName">The test group it is reported under.</param>
/// <param name="SortOrder">Where it sorts in the group, ascending.</param>
public sealed record HepLabTestType(int TestTypeId, string TestGroupName, int SortOrder);

/// <summary>
/// Builds the HepLab dataset of a case whose condition is in a hepatitis lab
/// group: its lab rows whose test type the group carries, in the group's sort
/// order, each carrying the test group it is reported under.
/// </summary>
/// <remarks>
/// The legacy hepatitis lab handling described in
/// <c>../nedss/docs/business-logic/NMI_XML_GENERATION.md</c> (the Hep Lab
/// dataset, grouped by test type) and
/// <c>../nedss/docs/business-logic/NMI_EXPORT_PIPELINE.md</c>, <i>Hepatitis
/// Lab Configuration</i>. Rows of one test type keep the order they came in.
/// </remarks>
public static class HepatitisLabGrouping
{
    /// <summary>The name of the dataset a rule set's <c>dataSet</c> data names to repeat over it.</summary>
    public const string DataSet = "HepLab";

    /// <summary>The field each HepLab row carries its test group in.</summary>
    public const string TestGroupField = "hep_test_group";

    /// <summary>The HepLab rows of <paramref name="labs"/> under the group's <paramref name="testTypes"/>.</summary>
    public static IReadOnlyList<DatasetRow> Group(IReadOnlyList<DatasetRow> labs, IReadOnlyList<HepLabTestType> testTypes)
    {
        var byId = testTypes.ToDictionary(item => item.TestTypeId);

        return labs
            .Select(row => (Row: row, TestType: LabOverrides.Id(row, LabOverrides.TestTypeField) is { } id
                ? byId.GetValueOrDefault(id)
                : null))
            .Where(item => item.TestType is not null)
            .OrderBy(item => item.TestType!.SortOrder)
            .Select(item => new DatasetRow(new Dictionary<string, string?>(item.Row.Fields, StringComparer.Ordinal)
            {
                [TestGroupField] = item.TestType!.TestGroupName,
            }))
            .ToList();
    }
}
