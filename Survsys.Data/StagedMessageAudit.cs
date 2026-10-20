namespace Survsys.Data;

/// <summary>
/// One state a staged message entered: the review state, the exception
/// category and detail it carried there, and when. The legacy
/// <c>system_messages_audits</c> row, which recorded each queue a message
/// passed through.
/// </summary>
/// <remarks>
/// Written by <see cref="SurvsysDbContext"/> in the save that queues or moves
/// a message, and by hand by the bulk updates that bypass the change tracker,
/// so a message's trail is every state it was in, in order.
/// </remarks>
public class StagedMessageAudit
{
    public int Id { get; set; }

    /// <summary>The staged message this state belongs to.</summary>
    public int StagedMessageId { get; set; }

    /// <summary>The review-state code the message entered, read against <see cref="MessageReviewStatusRules"/>.</summary>
    public string ReviewStatus { get; set; } = string.Empty;

    /// <summary>The exception category it carried there, read against <see cref="MessageExceptionRules"/>, or null.</summary>
    public string? ExceptionCategory { get; set; }

    /// <summary>The exception detail it carried there, or null.</summary>
    public string? ExceptionDetail { get; set; }

    /// <summary>When the message entered the state, in UTC.</summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>The audit row recording <paramref name="message"/> as it now stands.</summary>
    public static StagedMessageAudit Of(StagedMessage message, DateTime occurredAt)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new StagedMessageAudit
        {
            StagedMessageId = message.Id,
            ReviewStatus = message.ReviewStatus,
            ExceptionCategory = message.ExceptionCategory,
            ExceptionDetail = message.ExceptionDetail,
            OccurredAt = occurredAt,
        };
    }
}
