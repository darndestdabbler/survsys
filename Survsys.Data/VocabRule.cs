namespace Survsys.Data;

/// <summary>
/// A rule a condition applies to a message at intake: a message-filter rule,
/// which says whether a message is recent enough to act on, or a
/// case-management rule, which says whether it may open a new case, update an
/// existing one, and whether that case is surveillance. Its conditions hang off
/// its id in <see cref="VocabRuleCondition"/>.
/// </summary>
/// <remarks>
/// The legacy message filter and the <c>vocab_rules_masterloinc</c> and
/// <c>vocab_rules_mastersnomed</c> tables, as
/// <c>../nedss/docs/business-logic/EMSA_RULES_ENGINE.md</c> describes them,
/// kept on the condition rather than on the master LOINC and SNOMED: the
/// result's LOINC, organism and interpretation are fields a rule's conditions
/// read, so the rules are the ones on the condition the result resolved to.
/// The conditions replace the legacy JSON <c>conditions_structured</c> and its
/// free-text filter with the AND/OR conditions the ruleset expressions use.
/// The three flags mean something only on a case-management rule.
/// <see cref="ConditionId"/> is a bare id into <c>conditions</c>. There is no
/// behaviour here and no navigation out.
/// </remarks>
public class VocabRule
{
    /// <summary>The code a message-filter rule's <see cref="Kind"/> holds.</summary>
    public const string MessageFilterKind = "message_filter";

    /// <summary>The code a case-management rule's <see cref="Kind"/> holds.</summary>
    public const string CaseManagementKind = "case_management";

    public int Id { get; set; }

    /// <summary>The condition this rule belongs to, as a bare id into <c>conditions</c>.</summary>
    public int ConditionId { get; set; }

    /// <summary>The kind: <see cref="MessageFilterKind"/> or <see cref="CaseManagementKind"/>.</summary>
    public required string Kind { get; set; }

    /// <summary>The order the rule is listed and evaluated in, ascending.</summary>
    public int ExecutionOrder { get; set; }

    /// <summary>Whether a message this rule matches may open a new case.</summary>
    public bool AllowNewCase { get; set; }

    /// <summary>Whether a message this rule matches may update an existing case.</summary>
    public bool AllowUpdateCase { get; set; }

    /// <summary>Whether the case a message this rule matches opens is a surveillance case.</summary>
    public bool IsSurveillance { get; set; }
}
