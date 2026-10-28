using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The repeating-group processor: several Lab rows each produce one instance
/// with an incrementing number and the row's formatted value, an empty dataset
/// produces no instances, and an unsupported dataset name is rejected.
/// </summary>
public class RepeatingGroupProcessorTests
{
    // A string passthrough over the row's "result" field, so each row's value
    // identifies its instance.
    private static RepeatingGroup LabGroup(string dataSet = "Lab")
    {
        var rule = new LoadedRuleSet(
            TypeLabel: "fieldValueMap",
            FormatType: "string",
            Repeating: false,
            ValueSetName: null,
            Fields: ["result"],
            Data: new Dictionary<string, string?>(),
            ValueMaps: [],
            Expressions: [],
            ValueSet: []);
        return new RepeatingGroup(
            GroupName: "labs",
            DataSet: dataSet,
            Variables: [new RepeatingGroupVariable("LAB1", rule, new FieldValueMapFormatter())]);
    }

    private static EpiTraxRecord RecordWithLabRows(params string[] results)
    {
        var rows = results
            .Select(r => new DatasetRow(new Dictionary<string, string?> { ["result"] = r }))
            .ToList();
        return new EpiTraxRecord(
            new Dictionary<string, string?>(),
            new Dictionary<string, IReadOnlyList<DatasetRow>> { ["Lab"] = rows });
    }

    [Fact]
    public void EachRowProducesOneInstanceWithAnIncrementingNumber()
    {
        var instances = new RepeatingGroupProcessor().Process(
            LabGroup(),
            RecordWithLabRows("r1", "r2", "r3"));

        Assert.Equal([1, 2, 3], instances.Select(i => i.InstanceNumber));
        Assert.Equal(
            ["r1", "r2", "r3"],
            instances.Select(i => Assert.Single(i.Values).Formatted.Single().Value));
    }

    [Fact]
    public void AnEmptyDatasetProducesNoInstances()
    {
        var instances = new RepeatingGroupProcessor().Process(LabGroup(), RecordWithLabRows());

        Assert.Empty(instances);
    }

    [Fact]
    public void TheHepLabDatasetRepeatsLikeAnyOther()
    {
        var rows = new List<DatasetRow> { new(new Dictionary<string, string?> { ["result"] = "h1" }) };
        var record = new EpiTraxRecord(
            new Dictionary<string, string?>(),
            new Dictionary<string, IReadOnlyList<DatasetRow>> { [HepatitisLabGrouping.DataSet] = rows });

        var instance = Assert.Single(new RepeatingGroupProcessor().Process(LabGroup(HepatitisLabGrouping.DataSet), record));

        Assert.Equal("h1", Assert.Single(instance.Values).Formatted.Single().Value);
    }

    [Fact]
    public void AnUnsupportedDatasetNameIsRejected()
    {
        var processor = new RepeatingGroupProcessor();
        var record = RecordWithLabRows("r1");

        Assert.Throws<ArgumentException>(() => processor.Process(LabGroup("Bogus"), record));
    }
}
