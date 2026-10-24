namespace Survsys.Data;

/// <summary>
/// The review state an inbound message sits in while it waits in the intake
/// queue: the one of these a staged message's review status holds.
/// </summary>
/// <remarks>
/// The legacy EMSA queue state machine, as
/// <c>../nedss/docs/business-logic/EMSA_MESSAGE_PIPELINE.md</c> dumps it on
/// the <c>system_statuses</c> table, with the legacy queue id beside each
/// state, and <see cref="Filtered"/>, where the legacy deleted a message its
/// condition's filter refused and this keeps it out of the queue instead.
/// Intake writes <see cref="Entry"/>, <see cref="Exception"/> and
/// <see cref="Filtered"/>; automatic assignment writes <see cref="Assigned"/>,
/// <see cref="Pending"/>, <see cref="OutOfState"/> and <see cref="Gray"/>; the
/// graylist writes <see cref="Pending"/> on a release and
/// <see cref="GrayUnprocessable"/> on a prune. The rest are states a reviewer
/// moves a message into and out of.
/// </remarks>
public enum MessageReviewStatus
{
    /// <summary>
    /// Newly received and queued for review. Where a parseable message lands
    /// at intake. The legacy <c>Entry</c>, queue 17.
    /// </summary>
    Entry,

    /// <summary>
    /// Held for an epidemiologist to look at before it goes further. The
    /// legacy <c>Pending</c>, queue 25.
    /// </summary>
    Pending,

    /// <summary>
    /// Processing failed and the message is held with the reason. Where a
    /// message that could not be parsed lands at intake. The legacy
    /// <c>Exception</c>, queue 3.
    /// </summary>
    Exception,

    /// <summary>
    /// Assigned to a case, by a reviewer's promotion or automatically, so it
    /// has left the review queue. The terminal state a message reaches, and
    /// the one it cannot be promoted from again. The legacy <c>Assigned</c>,
    /// queue 14.
    /// </summary>
    Assigned,

    /// <summary>
    /// Refused by its condition's message filter — too old to act on — and so
    /// out of the queue. A terminal state: the legacy deleted such a message,
    /// and this keeps it so what was received is still on file.
    /// </summary>
    Filtered,

    /// <summary>
    /// Waiting on a case record another user holds locked. The legacy
    /// <c>Locked</c>, queue 27.
    /// </summary>
    Locked,

    /// <summary>
    /// Held for a quality-assurance look before it is promoted. The legacy
    /// <c>QA</c>, queue 19, where an automated assignment could be sent.
    /// </summary>
    Qa,

    /// <summary>
    /// The patient lives outside the jurisdiction. The legacy <c>OOS</c>,
    /// queue 28.
    /// </summary>
    OutOfState,

    /// <summary>
    /// Entered by hand with the reviewer choosing the organism and condition.
    /// The legacy <c>Semi-Auto</c>, queue 24.
    /// </summary>
    SemiAuto,

    /// <summary>
    /// Received but never taken through processing. The legacy
    /// <c>Unprocessed</c>, queue 26.
    /// </summary>
    Unprocessed,

    /// <summary>
    /// Graylisted: a result with no case to attach to, waiting for one to
    /// appear. The legacy <c>Gray</c>, queue 2.
    /// </summary>
    Gray,

    /// <summary>A graylisted message awaiting evaluation. The legacy <c>Gray Pending</c>, queue 20.</summary>
    GrayPending,

    /// <summary>A graylisted message that has been dealt with. The legacy <c>Gray Processed</c>, queue 21.</summary>
    GrayProcessed,

    /// <summary>
    /// A graylisted message no case appeared for within its window. The legacy
    /// <c>Gray Unprocessable</c>, queue 22.
    /// </summary>
    GrayUnprocessable,

    /// <summary>A graylisted message whose evaluation failed. The legacy <c>Gray Exception</c>, queue 23.</summary>
    GrayException,
}

/// <summary>
/// The review-state vocabulary, and the reading of a stored code into one of
/// the states.
/// </summary>
/// <remarks>
/// The codes echo the EMSA status names rather than the legacy numeric queue
/// ids, because a code on a row reads as what it is rather than as a number
/// someone must look up. There is no empty state: a staged message is always
/// in one of the states, so an empty or absent code is refused here where a
/// case status reads an empty code as Unknown — an unqueued message is not a
/// state this queue has.
///
/// Pure, public and static in the shape <see cref="CaseStatusRules"/> has, and
/// takes no <c>DbContext</c>: a status code is a string the caller has in
/// hand, and what is here is the reading of it. Writers store one of the code
/// constants; the reading back is <see cref="Read"/>, and <see cref="Codes"/>
/// is every code in the order the states are declared.
/// </remarks>
public static class MessageReviewStatusRules
{
    /// <summary>The code <see cref="MessageReviewStatus.Entry"/> is stored under.</summary>
    public const string EntryCode = "entry";

    /// <summary>The code <see cref="MessageReviewStatus.Pending"/> is stored under.</summary>
    public const string PendingCode = "pending";

    /// <summary>The code <see cref="MessageReviewStatus.Exception"/> is stored under.</summary>
    public const string ExceptionCode = "exception";

    /// <summary>The code <see cref="MessageReviewStatus.Assigned"/> is stored under.</summary>
    public const string AssignedCode = "assigned";

    /// <summary>The code <see cref="MessageReviewStatus.Filtered"/> is stored under.</summary>
    public const string FilteredCode = "filtered";

    /// <summary>The code <see cref="MessageReviewStatus.Locked"/> is stored under.</summary>
    public const string LockedCode = "locked";

    /// <summary>The code <see cref="MessageReviewStatus.Qa"/> is stored under.</summary>
    public const string QaCode = "qa";

    /// <summary>The code <see cref="MessageReviewStatus.OutOfState"/> is stored under.</summary>
    public const string OutOfStateCode = "out-of-state";

    /// <summary>The code <see cref="MessageReviewStatus.SemiAuto"/> is stored under.</summary>
    public const string SemiAutoCode = "semi-auto";

    /// <summary>The code <see cref="MessageReviewStatus.Unprocessed"/> is stored under.</summary>
    public const string UnprocessedCode = "unprocessed";

    /// <summary>The code <see cref="MessageReviewStatus.Gray"/> is stored under.</summary>
    public const string GrayCode = "gray";

    /// <summary>The code <see cref="MessageReviewStatus.GrayPending"/> is stored under.</summary>
    public const string GrayPendingCode = "gray-pending";

    /// <summary>The code <see cref="MessageReviewStatus.GrayProcessed"/> is stored under.</summary>
    public const string GrayProcessedCode = "gray-processed";

    /// <summary>The code <see cref="MessageReviewStatus.GrayUnprocessable"/> is stored under.</summary>
    public const string GrayUnprocessableCode = "gray-unprocessable";

    /// <summary>The code <see cref="MessageReviewStatus.GrayException"/> is stored under.</summary>
    public const string GrayExceptionCode = "gray-exception";

    private static readonly (string Code, MessageReviewStatus Status)[] States =
    [
        (EntryCode, MessageReviewStatus.Entry),
        (PendingCode, MessageReviewStatus.Pending),
        (ExceptionCode, MessageReviewStatus.Exception),
        (AssignedCode, MessageReviewStatus.Assigned),
        (FilteredCode, MessageReviewStatus.Filtered),
        (LockedCode, MessageReviewStatus.Locked),
        (QaCode, MessageReviewStatus.Qa),
        (OutOfStateCode, MessageReviewStatus.OutOfState),
        (SemiAutoCode, MessageReviewStatus.SemiAuto),
        (UnprocessedCode, MessageReviewStatus.Unprocessed),
        (GrayCode, MessageReviewStatus.Gray),
        (GrayPendingCode, MessageReviewStatus.GrayPending),
        (GrayProcessedCode, MessageReviewStatus.GrayProcessed),
        (GrayUnprocessableCode, MessageReviewStatus.GrayUnprocessable),
        (GrayExceptionCode, MessageReviewStatus.GrayException),
    ];

    private static readonly Dictionary<string, MessageReviewStatus> ByCode =
        States.ToDictionary(state => state.Code, state => state.Status, StringComparer.Ordinal);

    /// <summary>Every code this vocabulary knows, in the order the states are declared.</summary>
    public static IReadOnlyList<string> Codes { get; } = States.Select(state => state.Code).ToList();

    /// <summary>
    /// The review state a stored code names.
    /// </summary>
    /// <remarks>
    /// Surrounding whitespace is trimmed before the code is matched, and the
    /// match is exact — the codes are stored codes and not display text, so
    /// <c>Entry</c> is not <c>entry</c>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The code is empty, absent, or not one this vocabulary knows. A staged
    /// message is always in one of the states, so a row holding anything else
    /// was written by something that did not go through this vocabulary.
    /// </exception>
    public static MessageReviewStatus Read(string? storedCode)
    {
        if (!string.IsNullOrWhiteSpace(storedCode) && ByCode.TryGetValue(storedCode.Trim(), out var status))
        {
            return status;
        }

        throw new InvalidOperationException(
            $"'{storedCode}' is not a message review status code this tier knows. A staged "
            + $"message's review status is one of {string.Join(", ", Codes)}. A row holding anything "
            + "else was written by something that did not go through this vocabulary.");
    }
}
