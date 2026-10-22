namespace Survsys.Data;

/// <summary>
/// A hepatitis lab group: the conditions whose notifications carry the
/// grouped hepatitis lab results, and the test types it groups.
/// </summary>
/// <remarks>
/// The legacy NMI <c>hep_lab_condition_group</c>, as described in
/// <c>../nedss/docs/business-logic/NMI_EXPORT_PIPELINE.md</c>, <i>Hepatitis
/// Lab Configuration</i>. Its conditions are <see cref="HepLabGroupCondition"/>
/// and its test types <see cref="HepLabGroupTestType"/>. There is no
/// behaviour here.
/// </remarks>
public class HepLabGroup
{
    /// <summary>The group's id. The key.</summary>
    public int Id { get; set; }

    /// <summary>The group's name (for example <c>Hepatitis B</c>), unique.</summary>
    public required string Name { get; set; }
}
