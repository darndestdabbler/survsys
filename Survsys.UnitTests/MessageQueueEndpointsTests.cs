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
            ExceptionCategory = MessageExceptionRules.UnparseableCode,
        };

        var response = MessageQueueEndpoints.MessageToResponse.Compile().Invoke(exceptioned);

        Assert.Equal(
            new StagedMessageResponse(
                9,
                "arup-drop/garbage.txt",
                new DateTime(2026, 3, 13, 9, 0, 0, DateTimeKind.Utc),
                MessageReviewStatusRules.ExceptionCode,
                true,
                "The message could not be parsed as HL7 v2.",
                MessageExceptionRules.UnparseableCode,
                Retryable: false),
            response);
    }

    /// <summary>
    /// A message held for a category that may be retried says so, so a
    /// reviewer sees which held messages a retry can help.
    /// </summary>
    [Fact]
    public void MessageToResponseCarriesARetryableCategory()
    {
        var held = Message();
        held.ReviewStatus = MessageReviewStatusRules.ExceptionCode;
        held.ExceptionDetail = "No result resolves to a condition.";
        held.ExceptionCategory = MessageExceptionRules.LoincNotMappedCode;

        var response = MessageQueueEndpoints.MessageToResponse.Compile().Invoke(held);

        Assert.Equal(MessageExceptionRules.LoincNotMappedCode, response.ExceptionCategory);
        Assert.True(response.Retryable);
    }

    [Fact]
    public void RetryableCategoriesAreTheCategoriesMarkedRetryable()
    {
        Assert.Equal(
            MessageExceptionRules.Categories.Where(category => category.Retryable).Select(category => category.Code),
            MessageQueueEndpoints.RetryableCategories);
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

    /// <summary>
    /// A message waiting for review can be promoted: Entry and Pending are the
    /// states a reviewer promotes from, so the rule finds nothing wrong with
    /// them and the endpoint goes on to open the case. Surrounding whitespace
    /// is trimmed before the code is read, as a stored value can carry it.
    /// </summary>
    [Theory]
    [InlineData(MessageReviewStatusRules.EntryCode)]
    [InlineData(MessageReviewStatusRules.PendingCode)]
    [InlineData(MessageReviewStatusRules.SemiAutoCode)]
    [InlineData(MessageReviewStatusRules.QaCode)]
    [InlineData(" entry ")]
    public void WhyNotPromotablePassesAMessageWaitingForReview(string reviewStatus)
    {
        Assert.Null(MessageQueueEndpoints.WhyNotPromotable(reviewStatus));
    }

    /// <summary>
    /// The states promotion claims a message from are exactly the four the
    /// rule passes.
    /// </summary>
    [Fact]
    public void PromotableCodesAreTheStatesWhyNotPromotablePasses()
    {
        Assert.Equal(
            MessageReviewStatusRules.Codes.Where(code => MessageQueueEndpoints.WhyNotPromotable(code) is null).Order(),
            MessageQueueEndpoints.PromotableCodes.Order());
    }

    /// <summary>
    /// A message that never parsed cannot be promoted: there is nothing to
    /// open a case from, so the rule refuses it and names the exception so a
    /// reviewer reads why.
    /// </summary>
    [Fact]
    public void WhyNotPromotableRefusesAnExceptionedMessage()
    {
        var why = MessageQueueEndpoints.WhyNotPromotable(MessageReviewStatusRules.ExceptionCode);

        Assert.NotNull(why);
        Assert.Contains("exception", why, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A message already promoted cannot be promoted again: it is already a
    /// case, so the rule refuses a second report and says so.
    /// </summary>
    [Fact]
    public void WhyNotPromotableRefusesAnAlreadyAssignedMessage()
    {
        var why = MessageQueueEndpoints.WhyNotPromotable(MessageReviewStatusRules.AssignedCode);

        Assert.NotNull(why);
        Assert.Contains("already", why, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A state that is not waiting for promotion, and a code the review
    /// vocabulary does not know, are refused rather than treated as
    /// promotable, and the message names the value so the row holding it can
    /// be found.
    /// </summary>
    [Theory]
    [InlineData(MessageReviewStatusRules.LockedCode)]
    [InlineData(MessageReviewStatusRules.GrayCode)]
    [InlineData("parked")]
    public void WhyNotPromotableRefusesAStateNotWaitingForPromotion(string reviewStatus)
    {
        var why = MessageQueueEndpoints.WhyNotPromotable(reviewStatus);

        Assert.NotNull(why);
        Assert.Contains(reviewStatus, why, StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAnActionPassesBetweenOneAndTheMostIds()
    {
        Assert.Null(MessageQueueEndpoints.WhyNotAnAction(new StagedMessageActionRequest([1])));
        Assert.Null(MessageQueueEndpoints.WhyNotAnAction(
            new StagedMessageActionRequest(Enumerable.Range(1, MessageQueueEndpoints.MaxActionIds).ToList())));
    }

    [Fact]
    public void WhyNotAnActionRefusesNoIdsAndTooMany()
    {
        Assert.NotNull(MessageQueueEndpoints.WhyNotAnAction(new StagedMessageActionRequest([])));
        Assert.NotNull(MessageQueueEndpoints.WhyNotAnAction(new StagedMessageActionRequest(null!)));
        Assert.NotNull(MessageQueueEndpoints.WhyNotAnAction(
            new StagedMessageActionRequest(Enumerable.Range(1, MessageQueueEndpoints.MaxActionIds + 1).ToList())));
    }

    [Theory]
    [InlineData(MessageReviewStatusRules.EntryCode, MessageReviewStatusRules.PendingCode)]
    [InlineData(MessageReviewStatusRules.ExceptionCode, MessageReviewStatusRules.SemiAutoCode)]
    [InlineData(MessageReviewStatusRules.GrayCode, " gray-processed ")]
    [InlineData(MessageReviewStatusRules.FilteredCode, MessageReviewStatusRules.EntryCode)]
    public void WhyNotMovablePassesAMoveBetweenKnownStates(string from, string to)
    {
        Assert.Null(MessageQueueEndpoints.WhyNotMovable(from, to));
    }

    /// <summary>
    /// Nothing moves into or out of assigned by hand, and a target that is not
    /// a state is refused naming it.
    /// </summary>
    [Theory]
    [InlineData(MessageReviewStatusRules.EntryCode, MessageReviewStatusRules.AssignedCode)]
    [InlineData(MessageReviewStatusRules.AssignedCode, MessageReviewStatusRules.PendingCode)]
    [InlineData(MessageReviewStatusRules.EntryCode, "parked")]
    [InlineData(MessageReviewStatusRules.EntryCode, "")]
    [InlineData(MessageReviewStatusRules.EntryCode, null)]
    public void WhyNotMovableRefusesAssignedAndUnknownStates(string from, string? to)
    {
        Assert.NotNull(MessageQueueEndpoints.WhyNotMovable(from, to));
    }

    [Theory]
    [InlineData(MessageReviewStatusRules.EntryCode)]
    [InlineData(MessageReviewStatusRules.ExceptionCode)]
    [InlineData(MessageReviewStatusRules.GrayUnprocessableCode)]
    public void WhyNotDeletablePassesAMessageThatIsNotACase(string reviewStatus)
    {
        Assert.Null(MessageQueueEndpoints.WhyNotDeletable(reviewStatus));
    }

    [Fact]
    public void WhyNotDeletableRefusesAnAssignedMessage()
    {
        Assert.NotNull(MessageQueueEndpoints.WhyNotDeletable(MessageReviewStatusRules.AssignedCode));
    }

    [Theory]
    [InlineData(MessageReviewStatusRules.ExceptionCode)]
    [InlineData(MessageReviewStatusRules.UnprocessedCode)]
    [InlineData(MessageReviewStatusRules.LockedCode)]
    [InlineData(MessageReviewStatusRules.GrayExceptionCode)]
    public void WhyNotRetryablePassesAHeldParsedMessage(string reviewStatus)
    {
        Assert.Null(MessageQueueEndpoints.WhyNotRetryable(reviewStatus, parsed: true));
    }

    [Theory]
    [InlineData(MessageReviewStatusRules.EntryCode)]
    [InlineData(MessageReviewStatusRules.PendingCode)]
    [InlineData(MessageReviewStatusRules.AssignedCode)]
    [InlineData(MessageReviewStatusRules.GrayCode)]
    public void WhyNotRetryableRefusesAMessageThatIsNotHeld(string reviewStatus)
    {
        Assert.NotNull(MessageQueueEndpoints.WhyNotRetryable(reviewStatus, parsed: true));
    }

    [Fact]
    public void WhyNotRetryableRefusesAMessageThatNeverParsed()
    {
        var why = MessageQueueEndpoints.WhyNotRetryable(MessageReviewStatusRules.ExceptionCode, parsed: false);

        Assert.NotNull(why);
        Assert.Contains("never parsed", why, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(MessageExceptionRules.LoincNotMappedCode)]
    [InlineData(MessageExceptionRules.FailedToAddOrUpdateCode)]
    public void WhyNotRetryablePassesARetryableCategoryOrNone(string? category)
    {
        Assert.Null(MessageQueueEndpoints.WhyNotRetryable(MessageReviewStatusRules.ExceptionCode, parsed: true, category));
    }

    [Theory]
    [InlineData(MessageExceptionRules.EntryQueueExceptionCode)]
    [InlineData(MessageExceptionRules.WhitelistRuleExceptionCode)]
    [InlineData(MessageExceptionRules.UnparseableCode)]
    public void WhyNotRetryableRefusesACategoryAPersonResolves(string category)
    {
        var why = MessageQueueEndpoints.WhyNotRetryable(MessageReviewStatusRules.ExceptionCode, parsed: true, category);

        Assert.NotNull(why);
        Assert.Contains(category, why, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(MessageReviewStatusRules.ExceptionCode, true)]
    [InlineData(MessageReviewStatusRules.UnprocessedCode, true)]
    [InlineData(MessageReviewStatusRules.LockedCode, true)]
    [InlineData(MessageReviewStatusRules.GrayExceptionCode, true)]
    [InlineData(MessageReviewStatusRules.PendingCode, true)]
    [InlineData(MessageReviewStatusRules.EntryCode, false)]
    [InlineData(MessageReviewStatusRules.SemiAutoCode, false)]
    [InlineData(MessageReviewStatusRules.QaCode, false)]
    [InlineData(MessageReviewStatusRules.GrayCode, false)]
    public void KeepsWhyKeepsTheReasonOnlyForAHeldOrPendingState(string status, bool keeps)
    {
        Assert.Equal(keeps, MessageQueueEndpoints.KeepsWhy(status));
    }
}
