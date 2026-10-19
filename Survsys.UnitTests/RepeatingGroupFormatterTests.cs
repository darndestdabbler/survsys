using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The repeating-group formatter placeholder: called directly it returns one
/// warning result and no value, because repeating groups are expanded by the
/// processor rather than the formatter pipeline.
/// </summary>
public class RepeatingGroupFormatterTests
{
    [Fact]
    public void ReturnsAWarningWhenCalledDirectly()
    {
        var rule = new LoadedRuleSet(
            TypeLabel: "repeatingGroup",
            FormatType: null,
            Repeating: false,
            ValueSetName: null,
            Fields: [],
            Data: new Dictionary<string, string?>(),
            ValueMaps: [],
            Expressions: [],
            ValueSet: []);
        var record = new EpiTraxRecord(new Dictionary<string, string?>());

        var only = Assert.Single(new RepeatingGroupFormatter().Format(rule, record));

        Assert.Null(only.Value);
        Assert.Equal(RuleSetMessageKind.Warning, only.MessageKind);
        Assert.False(string.IsNullOrWhiteSpace(only.Message));
    }
}
