namespace Survsys.Data;

/// <summary>
/// The review state an inbound message sits in while it waits in the intake
/// queue: the one of these a staged message's review status holds.
/// </summary>
/// <remarks>
/// The legacy EMSA queue state machine, as
/// <c>../nedss/docs/business-logic/EMSA_MESSAGE_PIPELINE.md</c> dumps it on
/// the <c>system_statuses</c> table, reduced to the three this phase needs:
/// <see cref="Entry"/> (17), <see cref="Pending"/> (25) and
/// <see cref="Exception"/> (3). The many other legacy states — the graylist
/// cluster, assigned, locked, out of state and the rest — belong to the
/// assignment and review work a later session does, and are not here because
/// nothing this session writes or reads them.
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
}

/// <summary>
/// The review-state vocabulary, and the reading of a stored code into one of
/// the three states.
/// </summary>
/// <remarks>
/// The codes echo the EMSA status names rather than the legacy numeric queue
/// ids, because a code on a row reads as what it is rather than as a number
/// someone must look up. There is no empty state: a staged message is always
/// in one of the three, so an empty or absent code is refused here where a
/// case status reads an empty code as Unknown — an unqueued message is not a
/// state this queue has.
///
/// Pure, public and static in the shape <see cref="CaseStatusRules"/> has, and
/// takes no <c>DbContext</c>: a status code is a string the caller has in
/// hand, and what is here is the reading of it. The intake service writes a
/// code through <see cref="EntryCode"/> and <see cref="ExceptionCode"/>; the
/// reading back is <see cref="Read"/>.
/// </remarks>
public static class MessageReviewStatusRules
{
    /// <summary>The code <see cref="MessageReviewStatus.Entry"/> is stored under.</summary>
    public const string EntryCode = "entry";

    /// <summary>The code <see cref="MessageReviewStatus.Pending"/> is stored under.</summary>
    public const string PendingCode = "pending";

    /// <summary>The code <see cref="MessageReviewStatus.Exception"/> is stored under.</summary>
    public const string ExceptionCode = "exception";

    /// <summary>
    /// The review state a stored code names.
    /// </summary>
    /// <remarks>
    /// Surrounding whitespace is trimmed before the code is matched, and the
    /// match is exact — the codes are stored codes and not display text, so
    /// <c>Entry</c> is not <c>entry</c>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The code is empty, absent, or not one of the three this vocabulary
    /// knows. A staged message is always in one of the three states, so a row
    /// holding anything else was written by something that did not go through
    /// this vocabulary.
    /// </exception>
    public static MessageReviewStatus Read(string? storedCode)
    {
        if (!string.IsNullOrWhiteSpace(storedCode))
        {
            switch (storedCode.Trim())
            {
                case EntryCode:
                    return MessageReviewStatus.Entry;
                case PendingCode:
                    return MessageReviewStatus.Pending;
                case ExceptionCode:
                    return MessageReviewStatus.Exception;
            }
        }

        throw new InvalidOperationException(
            $"'{storedCode}' is not a message review status code this tier knows. A staged "
            + $"message's review status is one of {EntryCode}, {PendingCode} or "
            + $"{ExceptionCode}. A row holding anything else was written by something that "
            + "did not go through this vocabulary.");
    }
}
