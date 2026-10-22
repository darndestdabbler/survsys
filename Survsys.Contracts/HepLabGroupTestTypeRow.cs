namespace Survsys.Contracts;

/// <summary>A test type a hepatitis lab group carries.</summary>
/// <param name="TestTypeId">The <c>test_type</c> code value.</param>
/// <param name="TestGroupName">The test group it is reported under (for example <c>HBsAg</c>).</param>
/// <param name="SortOrder">Where it sorts in the group, ascending.</param>
public record HepLabGroupTestTypeRow(int TestTypeId, string TestGroupName, int SortOrder);
