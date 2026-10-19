namespace Survsys.Data;

/// <summary>
/// The link between a test type on the <c>test_type</c> code list and a
/// condition: which test types a case for that condition is offered.
/// </summary>
/// <remarks>
/// The legacy <c>lab_test_type_condition</c> table, as
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0006__more_init_condition_to_test_types_organism.sql</c>
/// under <c>../nedss</c> fills it — the two foreign keys and nothing else, as
/// <see cref="ConditionTreatment"/> is. The pair of ids is the key.
/// </remarks>
public class CodeValueCondition
{
    /// <summary>The test type, a <see cref="CodeValue"/> on the <c>test_type</c> list.</summary>
    public int CodeValueId { get; set; }

    /// <summary>The condition the test type is offered for.</summary>
    public int ConditionId { get; set; }
}
