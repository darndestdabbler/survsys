namespace Survsys.Data;

/// <summary>
/// The link between a condition and a treatment: which treatments a condition
/// offers, so a case for that condition is shown its relevant treatments.
/// </summary>
/// <remarks>
/// The columns are EpiTrax's own <c>condition_treatment</c> table, as declared
/// in <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c> — the two foreign keys and nothing else, so the row is
/// the link and carries no attributes of its own, as
/// <see cref="PersonConditionLab"/> is. There is no identity column and no
/// navigation out; the pair of ids is the key, and one treatment is listed for
/// one condition once.
/// </remarks>
public class ConditionTreatment
{
    /// <summary>The condition a treatment is listed for.</summary>
    public int ConditionId { get; set; }

    /// <summary>The treatment listed for the condition.</summary>
    public int TreatmentId { get; set; }
}
