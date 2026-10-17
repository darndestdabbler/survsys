using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The message review-state vocabulary: what each stored code reads back to,
/// and what the rule refuses to read.
/// </summary>
/// <remarks>
/// Pure throughout, so every case here is the rule itself and not a database
/// or a host. The states are the three this phase needs from the legacy EMSA
/// queue state machine, as
/// <c>../nedss/docs/business-logic/EMSA_MESSAGE_PIPELINE.md</c> lists them —
/// Entry, Pending and Exception. Unlike a case status there is no empty state:
/// a staged message is always in one of the three, so an empty or absent code
/// is among the values the rule refuses rather than a state of its own.
/// </remarks>
public class MessageReviewStatusRulesTests
{
    [Theory]
    [InlineData(MessageReviewStatusRules.EntryCode, MessageReviewStatus.Entry)]
    [InlineData(MessageReviewStatusRules.PendingCode, MessageReviewStatus.Pending)]
    [InlineData(MessageReviewStatusRules.ExceptionCode, MessageReviewStatus.Exception)]
    public void ReadNamesTheStateEachCodeIsStoredUnder(string code, MessageReviewStatus expected)
    {
        Assert.Equal(expected, MessageReviewStatusRules.Read(code));
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
    [InlineData("assigned")]
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
