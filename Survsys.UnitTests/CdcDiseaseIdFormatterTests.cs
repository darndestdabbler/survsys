using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The cdc-disease-id formatter: the placeholder returns one informational
/// result noting the condition's own ruleset stands in for it, and produces no
/// value.
/// </summary>
public class CdcDiseaseIdFormatterTests
{
    [Fact]
    public void ReturnsAnInformationalPlaceholderWithNoValue()
    {
        var rule = new LoadedRuleSet(
            TypeLabel: "cdcDiseaseId",
            FormatType: null,
            Repeating: false,
            ValueSetName: null,
            Fields: [],
            Data: new Dictionary<string, string?>(),
            ValueMaps: [],
            Expressions: [],
            ValueSet: []);
        var record = new EpiTraxRecord(new Dictionary<string, string?>());

        var only = Assert.Single(new CdcDiseaseIdFormatter().Format(rule, record));

        Assert.Null(only.Value);
        Assert.Equal(RuleSetMessageKind.Info, only.MessageKind);
        Assert.False(string.IsNullOrWhiteSpace(only.Message));
    }
}
