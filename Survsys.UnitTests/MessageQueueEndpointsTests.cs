using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the message-queue endpoints make of a stored message: the queue
/// summary the list and the detail both project it to.
/// </summary>
/// <remarks>
/// No database and no host: that the queue pages, filters by review state, and
/// reads one message whole, and that a missing message is a 404, are
/// <c>Survsys.IntegrationTests</c>'s to prove against a real SQL Server. What
/// is here is the projection itself — that the summary carries the message's
/// columns, and that the raw text is not among them, because the summary is
/// the list shape and the list leaves the raw text out.
/// </remarks>
public class MessageQueueEndpointsTests
{
    private static StagedMessage Message() => new()
    {
        Id = 7,
        RawMessage = "MSH|^~\\&|LAB|...",
        Source = "arup-drop/oru-0007.hl7",
        ReceivedAt = new DateTime(2026, 3, 13, 8, 7, 26, DateTimeKind.Utc),
        ReviewStatus = MessageReviewStatusRules.EntryCode,
        QaFlagged = false,
        ExceptionDetail = null,
    };

    [Fact]
    public void MessageToResponseCarriesTheMessagesSummaryColumns()
    {
        var response = MessageQueueEndpoints.MessageToResponse.Compile().Invoke(Message());

        Assert.Equal(
            new StagedMessageResponse(
                7,
                "arup-drop/oru-0007.hl7",
                new DateTime(2026, 3, 13, 8, 7, 26, DateTimeKind.Utc),
                MessageReviewStatusRules.EntryCode,
                false,
                null),
            response);
    }

    /// <summary>
    /// A message that failed to parse: it is in the Exception state, carries
    /// its detail, and — like every message — comes back with no raw text on
    /// the summary, because the raw text is the detail shape's, not the list's.
    /// </summary>
    [Fact]
    public void MessageToResponseCarriesAnExceptionedMessageWithoutItsRawText()
    {
        var exceptioned = new StagedMessage
        {
            Id = 9,
            RawMessage = "not an HL7 message",
            Source = "arup-drop/garbage.txt",
            ReceivedAt = new DateTime(2026, 3, 13, 9, 0, 0, DateTimeKind.Utc),
            ReviewStatus = MessageReviewStatusRules.ExceptionCode,
            QaFlagged = true,
            ExceptionDetail = "The message could not be parsed as HL7 v2.",
        };

        var response = MessageQueueEndpoints.MessageToResponse.Compile().Invoke(exceptioned);

        Assert.Equal(
            new StagedMessageResponse(
                9,
                "arup-drop/garbage.txt",
                new DateTime(2026, 3, 13, 9, 0, 0, DateTimeKind.Utc),
                MessageReviewStatusRules.ExceptionCode,
                true,
                "The message could not be parsed as HL7 v2."),
            response);
    }

    /// <summary>
    /// A message with no source recorded comes back with a null source rather
    /// than an empty string, because the column is nullable and a message
    /// dropped without one has none.
    /// </summary>
    [Fact]
    public void MessageToResponseCarriesAMessageWithNoSource()
    {
        var bare = new StagedMessage
        {
            Id = 11,
            ReceivedAt = new DateTime(2026, 3, 13, 10, 0, 0, DateTimeKind.Utc),
            ReviewStatus = MessageReviewStatusRules.EntryCode,
        };

        var response = MessageQueueEndpoints.MessageToResponse.Compile().Invoke(bare);

        Assert.Null(response.Source);
        Assert.Equal(11, response.Id);
        Assert.Equal(MessageReviewStatusRules.EntryCode, response.ReviewStatus);
    }
}
