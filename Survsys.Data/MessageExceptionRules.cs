namespace Survsys.Data;

/// <summary>
/// One kind of reason a message is held: its stored code, the legacy
/// <c>system_exceptions</c> id and name it carries over, and whether a message
/// held for it may be retried.
/// </summary>
public sealed record MessageExceptionCategory(string Code, int LegacyId, string Name, bool Retryable);

/// <summary>
/// The exception-category vocabulary: the kinds of reason a message is held as
/// an exception or sent to pending, and the reading of a stored code into one.
/// </summary>
/// <remarks>
/// The legacy EMSA <c>system_exceptions</c> table, as
/// <c>legacy-EMSA/database/release/populate_base_data.sql</c> under
/// <c>../nedss</c> seeds it, reduced to the categories this tier raises: the
/// Mirth parameters, master and application XML documents and SQL agent
/// responses the rest describe are plumbing this tier does not have. Each keeps
/// the legacy id and the legacy retry flag, except
/// <see cref="UnparseableCode"/>: a retry translates a message again and does
/// not parse it again, so one that never parsed has nothing to retry. The
/// legacy pending-queue categories, an entry-queue exception and a whitelist
/// rule exception, are a person's to resolve and are not retried.
///
/// Pure, public and static in the shape <see cref="MessageReviewStatusRules"/>
/// has: writers store one of the code constants, and <see cref="Read"/> reads
/// one back.
/// </remarks>
public static class MessageExceptionRules
{
    /// <summary>The message did not parse. The legacy <c>Could Not Map System</c>, 17.</summary>
    public const string UnparseableCode = "unparseable";

    /// <summary>The sending facility is not on file. The legacy <c>Lab Id Not Found For Lab Name</c>, 40.</summary>
    public const string UnknownSenderCode = "unknown-sender";

    /// <summary>No result resolves to a condition in the vocabulary. The legacy <c>LOINC Code</c>, 1.</summary>
    public const string LoincNotMappedCode = "loinc-not-mapped";

    /// <summary>A rule reads a field the message does not carry. The legacy <c>Missing Required Field</c>, 31.</summary>
    public const string MissingRequiredFieldCode = "missing-required-field";

    /// <summary>
    /// The condition has no message filter or case-management rule. The legacy
    /// <c>No Case Management Rules Found For Master LOINC</c>, 72.
    /// </summary>
    public const string NoCaseManagementRulesCode = "no-case-rules";

    /// <summary>
    /// Not exactly one case-management rule matches the result. The legacy
    /// <c>No Case Management Rules Evaluated True</c>, 73.
    /// </summary>
    public const string NoCaseManagementRuleTrueCode = "no-case-rule-true";

    /// <summary>Writing the case failed. The legacy <c>Failed to Add or Update</c>, 22.</summary>
    public const string FailedToAddOrUpdateCode = "failed-to-add-or-update";

    /// <summary>The patient's zip names no single agency. The legacy <c>Zip Code</c>, 3.</summary>
    public const string ZipCodeNotMappedCode = "zip-code";

    /// <summary>A person must decide where the message goes. The legacy <c>Entry Queue Exception</c>, 69.</summary>
    public const string EntryQueueExceptionCode = "entry-queue";

    /// <summary>
    /// The whitelist could not choose the case to update. The legacy
    /// <c>Whitelist Rule Exception</c>, 70.
    /// </summary>
    public const string WhitelistRuleExceptionCode = "whitelist-rule";

    /// <summary>Every category this vocabulary knows, in the order the codes are declared.</summary>
    public static IReadOnlyList<MessageExceptionCategory> Categories { get; } =
    [
        new(UnparseableCode, 17, "Could Not Map System", Retryable: false),
        new(UnknownSenderCode, 40, "Lab Id Not Found For Lab Name", Retryable: true),
        new(LoincNotMappedCode, 1, "LOINC Code", Retryable: true),
        new(MissingRequiredFieldCode, 31, "Missing Required Field", Retryable: true),
        new(NoCaseManagementRulesCode, 72, "No Case Management Rules Found For Master LOINC", Retryable: true),
        new(NoCaseManagementRuleTrueCode, 73, "No Case Management Rules Evaluated True", Retryable: true),
        new(FailedToAddOrUpdateCode, 22, "Failed to Add or Update", Retryable: true),
        new(ZipCodeNotMappedCode, 3, "Zip Code", Retryable: true),
        new(EntryQueueExceptionCode, 69, "Entry Queue Exception", Retryable: false),
        new(WhitelistRuleExceptionCode, 70, "Whitelist Rule Exception", Retryable: false),
    ];

    private static readonly Dictionary<string, MessageExceptionCategory> ByCode =
        Categories.ToDictionary(category => category.Code, StringComparer.Ordinal);

    /// <summary>Every code this vocabulary knows, in the order <see cref="Categories"/> lists them.</summary>
    public static IReadOnlyList<string> Codes { get; } = Categories.Select(category => category.Code).ToList();

    /// <summary>
    /// The category a stored code names. Surrounding whitespace is trimmed and
    /// the match is exact.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The code is empty, absent, or not one this vocabulary knows.
    /// </exception>
    public static MessageExceptionCategory Read(string? storedCode)
    {
        if (!string.IsNullOrWhiteSpace(storedCode) && ByCode.TryGetValue(storedCode.Trim(), out var category))
        {
            return category;
        }

        throw new InvalidOperationException(
            $"'{storedCode}' is not a message exception category this tier knows. A held message's "
            + $"category is one of {string.Join(", ", Codes)}.");
    }
}
