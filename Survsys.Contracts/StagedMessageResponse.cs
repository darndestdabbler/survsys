namespace Survsys.Contracts;

/// <summary>
/// One staged inbound message, as the API tier returns it in the intake-queue
/// list.
/// </summary>
/// <remarks>
/// The message's own fields a reviewer triages the queue by — where it came
/// from, when it arrived, which review state it sits in, whether it is flagged
/// for QA, and the detail of the exception that stopped it where one did. The
/// raw HL7 text a message carries is left out of the list, because it is large
/// and a queue shows many rows; it is <see cref="StagedMessageDetailResponse"/>'s,
/// read one message at a time.
///
/// The review state is the stored code, not a resolved name: the queue shows
/// the code and the review screen gives it its label, as the other slices
/// return a status code and resolve it on the screen.
/// </remarks>
/// <param name="Id">The message's identifier, as the detail endpoint refers to it.</param>
/// <param name="Source">
/// Where the message came from — the dropped file's name, or the connector a
/// lab sent under — or null where none was recorded.
/// </param>
/// <param name="ReceivedAt">When the message was received into the queue, in UTC.</param>
/// <param name="ReviewStatus">
/// The review state the message sits in, as the stored code the review-state
/// vocabulary reads.
/// </param>
/// <param name="QaFlagged">Whether the message is flagged for quality-assurance review.</param>
/// <param name="ExceptionDetail">
/// The detail of the exception that stopped the message, or null where none
/// did.
/// </param>
/// <param name="ExceptionCategory">
/// The kind of reason the message is held, as the stored category code, or
/// null where it is not held or none was recorded.
/// </param>
/// <param name="Retryable">
/// Whether the message's category lets it be retried; false where it has none.
/// </param>
public record StagedMessageResponse(
    int Id,
    string? Source,
    DateTime ReceivedAt,
    string ReviewStatus,
    bool QaFlagged,
    string? ExceptionDetail,
    string? ExceptionCategory = null,
    bool Retryable = false);
