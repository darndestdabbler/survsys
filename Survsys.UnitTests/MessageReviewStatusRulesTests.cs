using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The message review-state vocabulary: what each stored code reads back to,
/// and what the rule refuses to read.
/// </summary>
/// <remarks>
/// Pure throughout, so every case here is the rule itself and not a database
/// or a host. The states are the legacy EMSA queue state machine's, as
/// <c>../nedss/docs/business-logic/EMSA_MESSAGE_PIPELINE.md</c> lists them,
/// and the Filtered state a message its condition's filter refused is kept in.
/// Unlike a case status there is no empty state: a staged message is always in
/// one of them, so an empty or absent code is among the values the rule
/// refuses rather than a state of its own.
/// </remarks>
public class MessageReviewStatusRulesTests
{
    [Theory]
    [InlineData(MessageReviewStatusRules.EntryCode, MessageReviewStatus.Entry)]
    [InlineData(MessageReviewStatusRules.PendingCode, MessageReviewStatus.Pending)]
    [InlineData(MessageReviewStatusRules.ExceptionCode, MessageReviewStatus.Exception)]
    [InlineData(MessageReviewStatusRules.AssignedCode, MessageReviewStatus.Assigned)]
    [InlineData(MessageReviewStatusRules.FilteredCode, MessageReviewStatus.Filtered)]
    [InlineData(MessageReviewStatusRules.LockedCode, MessageReviewStatus.Locked)]
    [InlineData(MessageReviewStatusRules.QaCode, MessageReviewStatus.Qa)]
    [InlineData(MessageReviewStatusRules.OutOfStateCode, MessageReviewStatus.OutOfState)]
    [InlineData(MessageReviewStatusRules.SemiAutoCode, MessageReviewStatus.SemiAuto)]
    [InlineData(MessageReviewStatusRules.UnprocessedCode, MessageReviewStatus.Unprocessed)]
    [InlineData(MessageReviewStatusRules.GrayCode, MessageReviewStatus.Gray)]
    [InlineData(MessageReviewStatusRules.GrayPendingCode, MessageReviewStatus.GrayPending)]
    [InlineData(MessageReviewStatusRules.GrayProcessedCode, MessageReviewStatus.GrayProcessed)]
    [InlineData(MessageReviewStatusRules.GrayUnprocessableCode, MessageReviewStatus.GrayUnprocessable)]
    [InlineData(MessageReviewStatusRules.GrayExceptionCode, MessageReviewStatus.GrayException)]
    public void ReadNamesTheStateEachCodeIsStoredUnder(string code, MessageReviewStatus expected)
    {
        Assert.Equal(expected, MessageReviewStatusRules.Read(code));
    }

    /// <summary>
    /// Every state has exactly one code, listed in the order the states are
    /// declared, and every code fits the column it is stored in.
    /// </summary>
    [Fact]
    public void CodesListsOneCodePerStateInDeclarationOrder()
    {
        var states = MessageReviewStatusRules.Codes.Select(MessageReviewStatusRules.Read).ToList();

        Assert.Equal(Enum.GetValues<MessageReviewStatus>(), states);
        Assert.All(MessageReviewStatusRules.Codes, code => Assert.True(code.Length <= 20, code));
    }

    /// <summary>
    /// Surrounding whitespace is trimmed before a non-empty code is matched,
    /// as a stored value can carry padding.
    /// </summary>
    [Theory]
    [InlineData(" entry ", MessageReviewStatus.Entry)]
    [InlineData("\texception\n", MessageReviewStatus.Exception)]
    public void ReadTrimsSurroundingWhitespace(string code, MessageReviewStatus expected)
    {
        Assert.Equal(expected, MessageReviewStatusRules.Read(code));
    }

    /// <summary>
    /// What the reader refuses: an empty or absent code, because a staged
    /// message is always in a state; a code in the wrong case, because the
    /// codes are stored codes and not display text; and a value that is not a
    /// code at all. The message names the value, because the row holding it is
    /// what someone will have to go and look at.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Entry")]
    [InlineData("ENTRY")]
    [InlineData("Assigned")]
    [InlineData("Filtered")]
    [InlineData("17")]
    [InlineData("X")]
    public void ReadRefusesACodeTheVocabularyDoesNotKnow(string? storedCode)
    {
        var refused = Assert.Throws<InvalidOperationException>(
            () => MessageReviewStatusRules.Read(storedCode));

        if (!string.IsNullOrEmpty(storedCode))
        {
            Assert.Contains(storedCode, refused.Message, StringComparison.Ordinal);
        }
    }
}
