using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The notification-status-response formatter: a response value mapped through
/// the value map to a concept code and resolved in the value set, and the
/// repeating flag choosing between every response and only the first.
/// </summary>
public class NotificationStatusResponseFormatterTests
{
    private static readonly IReadOnlyList<ValueMapping> Maps =
    [
        new ValueMapping("Approved", "C1", null),
        new ValueMapping("Rejected", "C2", null),
    ];

    private static readonly IReadOnlyList<ValueSetConcept> ValueSet =
    [
        new ValueSetConcept("Approved for notification", "C1H", "C1", "2.16.840.1.1"),
        new ValueSetConcept("Rejected for notification", "C2H", "C2", "2.16.840.1.1"),
    ];

    private static LoadedRuleSet Rule(bool repeating)
        => new(
            TypeLabel: "notificationStatusResponse",
            FormatType: null,
            Repeating: repeating,
            ValueSetName: "response",
            Fields: ["response"],
            Data: new Dictionary<string, string?>(),
            ValueMaps: Maps,
            Expressions: [],
            ValueSet: ValueSet);

    [Fact]
    public void AResponseValueIsMappedToItsConcept()
    {
        var record = new EpiTraxRecord(new Dictionary<string, string?> { ["response"] = "Approved" });

        var only = Assert.Single(new NotificationStatusResponseFormatter().Format(Rule(repeating: false), record));

        Assert.Equal("Approved for notification", only.Value);
        Assert.Equal("C1", only.Id);
    }

    [Fact]
    public void RepeatingReturnsEveryResponse()
    {
        var record = new EpiTraxRecord(new Dictionary<string, string?> { ["response"] = "Approved|Rejected" });

        var result = new NotificationStatusResponseFormatter().Format(Rule(repeating: true), record);

        Assert.Equal(["Approved for notification", "Rejected for notification"], result.Select(r => r.Value));
    }

    [Fact]
    public void NotRepeatingReturnsOnlyTheFirstResponse()
    {
        var record = new EpiTraxRecord(new Dictionary<string, string?> { ["response"] = "Approved|Rejected" });

        var only = Assert.Single(new NotificationStatusResponseFormatter().Format(Rule(repeating: false), record));

        Assert.Equal("Approved for notification", only.Value);
    }
}
