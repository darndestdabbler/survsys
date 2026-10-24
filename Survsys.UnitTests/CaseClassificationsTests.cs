using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The circuit's last classification: that recording one keeps it and says so.
/// </summary>
public class CaseClassificationsTests
{
    [Fact]
    public void LastIsNullBeforeAnyIsRecorded()
    {
        Assert.Null(new CaseClassifications().Last);
    }

    [Fact]
    public void RecordKeepsTheLastClassificationAndRaisesClassified()
    {
        var classifications = new CaseClassifications();
        var raised = new List<CaseClassificationResponse>();
        classifications.Classified += raised.Add;

        var first = new CaseClassificationResponse(7, null, []);
        var second = new CaseClassificationResponse(7, "C", ["Confirmed."]);
        classifications.Record(first);
        classifications.Record(second);

        Assert.Same(second, classifications.Last);
        Assert.Equal([first, second], raised);
    }
}
