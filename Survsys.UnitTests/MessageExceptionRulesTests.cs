using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The message exception-category vocabulary: the legacy category each code
/// carries, whether it may be retried, and what the rule refuses to read.
/// </summary>
public class MessageExceptionRulesTests
{
    [Theory]
    [InlineData(MessageExceptionRules.UnparseableCode, 17, false)]
    [InlineData(MessageExceptionRules.UnknownSenderCode, 40, true)]
    [InlineData(MessageExceptionRules.LoincNotMappedCode, 1, true)]
    [InlineData(MessageExceptionRules.MissingRequiredFieldCode, 31, true)]
    [InlineData(MessageExceptionRules.NoCaseManagementRulesCode, 72, true)]
    [InlineData(MessageExceptionRules.NoCaseManagementRuleTrueCode, 73, true)]
    [InlineData(MessageExceptionRules.FailedToAddOrUpdateCode, 22, true)]
    [InlineData(MessageExceptionRules.ZipCodeNotMappedCode, 3, true)]
    [InlineData(MessageExceptionRules.EntryQueueExceptionCode, 69, false)]
    [InlineData(MessageExceptionRules.WhitelistRuleExceptionCode, 70, false)]
    public void ReadNamesTheLegacyCategoryAndWhetherItMayBeRetried(string code, int legacyId, bool retryable)
    {
        var category = MessageExceptionRules.Read(code);

        Assert.Equal(code, category.Code);
        Assert.Equal(legacyId, category.LegacyId);
        Assert.Equal(retryable, category.Retryable);
        Assert.False(string.IsNullOrWhiteSpace(category.Name));
    }

    /// <summary>
    /// Every category has one distinct code and legacy id, and every code fits
    /// the column it is stored in.
    /// </summary>
    [Fact]
    public void CodesListsOneDistinctCodePerCategory()
    {
        Assert.Equal(MessageExceptionRules.Categories.Select(category => category.Code), MessageExceptionRules.Codes);
        Assert.Equal(MessageExceptionRules.Codes.Count, MessageExceptionRules.Codes.Distinct().Count());
        Assert.Equal(
            MessageExceptionRules.Categories.Count,
            MessageExceptionRules.Categories.Select(category => category.LegacyId).Distinct().Count());
        Assert.All(MessageExceptionRules.Codes, code => Assert.True(code.Length <= 40, code));
    }

    [Fact]
    public void ReadTrimsSurroundingWhitespace()
    {
        Assert.Equal(
            MessageExceptionRules.ZipCodeNotMappedCode,
            MessageExceptionRules.Read(" zip-code ").Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Zip-Code")]
    [InlineData("sqla-error")]
    public void ReadRefusesACodeItDoesNotKnow(string? code)
    {
        Assert.Throws<InvalidOperationException>(() => MessageExceptionRules.Read(code));
    }
}
