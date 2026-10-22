using System.Globalization;
using Survsys.Data;
using Survsys.Messaging;

namespace Survsys.Api;

/// <summary>
/// A condition's intake rules, as the messaging module evaluates them: its
/// message-filter rules and its case-management rules, each in execution order.
/// </summary>
public sealed record IntakeConditionRules(
    IReadOnlyList<ConditionExpression> Filter,
    IReadOnlyList<CaseManagementRule> CaseManagement);

/// <summary>
/// What intake translates a message against: the vocabulary, each condition's
/// rules by condition id, and the zip-to-agency mapping.
/// </summary>
public sealed record IntakeReference(
    VocabularyMatcher Matcher,
    IReadOnlyDictionary<int, IntakeConditionRules> RulesByCondition,
    IReadOnlyList<ZipCodeAgency> ZipMapping);

/// <summary>
/// Translates a message intake has queued: each result through the vocabulary
/// to its master LOINC, condition, organism and interpretation; the message
/// through its condition's message filter and case-management rules; and the
/// patient's zip to the investigating agency.
/// </summary>
/// <remarks>
/// The vocabulary and the first two rules engines of
/// <c>EMSA_VOCABULARY_SYSTEM.md</c> and <c>EMSA_RULES_ENGINE.md</c>, applied
/// at intake. The message's condition is the first of its results that
/// resolved to one, and the rules read that result: a laboratory order commonly
/// carries results that are not reportable beside the one that is, so a result
/// the vocabulary does not carry is left untranslated rather than held. A
/// message none of whose results resolves, whose condition's filter or
/// case-management rules decide nothing, is held as an
/// <see cref="MessageReviewStatus.Exception"/> with why; one its filter refuses
/// is <see cref="MessageReviewStatus.Filtered"/>. The agency is set where the
/// zip resolves to one agency, whatever the rules decide, and left empty
/// otherwise.
///
/// Pure and static, as <see cref="ElrIntakeService.Stage"/> is: the caller
/// loads the reference and saves the message, so the unit suite proves every
/// outcome without a database.
/// </remarks>
public static class IntakeTranslator
{
    /// <summary>The field a rule reads the result's master LOINC from.</summary>
    public const string LoincField = "Loinc";

    /// <summary>The field a rule reads the result's interpretation from.</summary>
    public const string TestResultField = "TestResult";

    /// <summary>The field a rule reads the result's value (OBX-5) from.</summary>
    public const string ResultValueField = "ResultValue";

    /// <summary>The field a rule reads the result's organism name from.</summary>
    public const string OrganismField = "Organism";

    /// <summary>The field a rule reads the specimen's application value from.</summary>
    public const string SpecimenField = "Specimen";

    /// <summary>The field a rule reads the days from collection to today from.</summary>
    public const string DaysSinceCollectionField = "DaysSinceCollection";

    private static readonly IntakeConditionRules NoRules = new([], []);

    /// <summary>
    /// Translates <paramref name="message"/>, from the sending facility
    /// <paramref name="labId"/>, against <paramref name="reference"/> on
    /// <paramref name="today"/>. A message not in
    /// <see cref="MessageReviewStatus.Entry"/> — one already held — is left as
    /// it is.
    /// </summary>
    public static void Translate(StagedMessage message, int labId, IntakeReference reference, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(reference);

        if (message.ReviewStatus != MessageReviewStatusRules.EntryCode)
        {
            return;
        }

        var zip = ZipAgencyRules.Resolve(message.PatientZip, reference.ZipMapping);
        message.AgencyId = zip.Outcome == ZipResolutionOutcome.Resolved ? zip.AgencyId : null;

        var matcher = reference.Matcher;
        VocabularyMatch? decisiveMatch = null;
        StagedMessageResult? decisive = null;

        foreach (var result in message.Results.OrderBy(result => result.Sequence))
        {
            var match = matcher.MatchResult(result.ObservationCode, result.ResultValue, labId);
            if (!match.Matched)
            {
                continue;
            }

            result.MasterLoinc = match.MasterLoinc;
            result.ConditionId = match.ConditionId;
            result.OrganismId = match.OrganismId;
            result.Interpretation =
                matcher.MatchValue(VocabularyCategory.TestResult, result.ResultValue, labId)
                ?? matcher.MatchValue(VocabularyCategory.TestResult, result.CodedResult, labId);

            if (decisive is null && match.ConditionId is not null)
            {
                decisive = result;
                decisiveMatch = match;
            }
        }

        if (decisive is null || decisiveMatch is null)
        {
            Hold(
                message,
                MessageExceptionRules.LoincNotMappedCode,
                $"No result of this message resolves to a condition in the vocabulary for sending facility "
                + $"'{message.SendingFacility}': add its LOINC, or the lab's child LOINC, to the vocabulary.");
            return;
        }

        var conditionId = decisive.ConditionId!.Value;
        message.ConditionId = conditionId;

        var record = new EpiTraxRecord(new Dictionary<string, string?>
        {
            [LoincField] = decisive.MasterLoinc,
            [TestResultField] = decisive.Interpretation,
            [ResultValueField] = decisive.ResultValue,
            [OrganismField] = decisiveMatch.OrganismName,
            [SpecimenField] = matcher.MatchValue(VocabularyCategory.Specimen, message.SpecimenSourceCode, labId)
                ?? matcher.MatchValue(VocabularyCategory.Specimen, message.SpecimenSourceText, labId),
            [DaysSinceCollectionField] = message.CollectedOn is { } collected
                ? (today.DayNumber - collected.DayNumber).ToString(CultureInfo.InvariantCulture)
                : null,
        });

        var rules = reference.RulesByCondition.GetValueOrDefault(conditionId) ?? NoRules;

        switch (IntakeRules.Filter(rules.Filter, record))
        {
            case FilterOutcome.NoRules:
                Hold(message, MessageExceptionRules.NoCaseManagementRulesCode, $"Condition {conditionId} has no message filter rule, so whether to act on "
                    + "this message is a person's to decide.");
                return;
            case FilterOutcome.MissingField:
                Hold(message, MessageExceptionRules.MissingRequiredFieldCode, $"A message filter rule of condition {conditionId} reads a field this message "
                    + "does not carry, such as its collection date.");
                return;
            case FilterOutcome.Filtered:
                message.ReviewStatus = MessageReviewStatusRules.FilteredCode;
                return;
        }

        var decision = IntakeRules.CaseManagement(rules.CaseManagement, record);
        switch (decision.Outcome)
        {
            case CaseManagementOutcome.Decided:
                message.AllowNewCase = decision.Rule!.AllowNewCase;
                message.AllowUpdateCase = decision.Rule.AllowUpdateCase;
                message.IsSurveillance = decision.Rule.IsSurveillance;
                return;
            case CaseManagementOutcome.NoRules:
                Hold(message, MessageExceptionRules.NoCaseManagementRulesCode, $"Condition {conditionId} has no case-management rule.");
                return;
            case CaseManagementOutcome.NoneMatched:
                Hold(message, MessageExceptionRules.NoCaseManagementRuleTrueCode, $"No case-management rule of condition {conditionId} matches this message's "
                    + $"result (interpretation '{decisive.Interpretation}').");
                return;
            default:
                Hold(message, MessageExceptionRules.NoCaseManagementRuleTrueCode, $"More than one case-management rule of condition {conditionId} matches this "
                    + $"message's result (interpretation '{decisive.Interpretation}'); exactly one must.");
                return;
        }
    }

    private static void Hold(StagedMessage message, string category, string reason)
    {
        message.ReviewStatus = MessageReviewStatusRules.ExceptionCode;
        message.ExceptionDetail = reason;
        message.ExceptionCategory = category;
    }
}
