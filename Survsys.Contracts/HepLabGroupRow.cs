namespace Survsys.Contracts;

/// <summary>
/// A hepatitis lab group whole, as
/// <c>/api/notification-setup/hep-lab-groups</c> lists and saves it.
/// </summary>
/// <param name="Id">The group's id; ignored when one is created.</param>
/// <param name="Name">The group's name, unique.</param>
/// <param name="ConditionIds">The conditions whose notifications carry the group's labs; a condition is in one group.</param>
/// <param name="TestTypes">The test types the group carries, with their test group and sort order.</param>
public record HepLabGroupRow(
    int Id,
    string Name,
    IReadOnlyList<int> ConditionIds,
    IReadOnlyList<HepLabGroupTestTypeRow> TestTypes);
