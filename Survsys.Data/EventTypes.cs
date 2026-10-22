namespace Survsys.Data;

/// <summary>
/// The kinds of event a <see cref="PersonCondition"/> row can be, and the one
/// rule the vocabulary needs of itself: whether a code is one of them.
/// </summary>
/// <remarks>
/// The legacy <c>person_condition</c> table holds morbidity reports, contacts
/// and assessments alike, distinguished by <c>person_condition_type_id</c>
/// into the <c>person_condition_type</c> table that
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c> declares. There is no such table here: the three
/// kinds are a fixed vocabulary that code branches on, so they live here and
/// the <c>event_type</c> column holds the code, as <see cref="WorkflowRules"/>
/// and <see cref="CaseStatusRules"/> hold theirs.
///
/// A morbidity report is the case an investigator opens; a contact is a person
/// exposed to a case, opened from that case's exposure and promoted to a
/// morbidity report if they turn out to have the condition; an assessment is
/// opened by hand, as a morbidity report is, for a person being looked at
/// without yet being a case.
/// </remarks>
public static class EventTypes
{
    /// <summary>A case morbidity report — every case written before event types existed.</summary>
    public const string Morbidity = "morbidity";

    /// <summary>A contact of a case, opened from an exposure.</summary>
    public const string Contact = "contact";

    /// <summary>An assessment.</summary>
    public const string Assessment = "assessment";

    /// <summary>The three codes, in the order a screen offers them.</summary>
    public static IReadOnlyList<string> All { get; } = [Morbidity, Contact, Assessment];

    /// <summary>
    /// Whether <paramref name="code"/> is one of the three, exactly as stored:
    /// the codes are lowercase and are compared as they are written.
    /// </summary>
    public static bool IsKnown(string? code) =>
        code is not null && All.Contains(code, StringComparer.Ordinal);
}
