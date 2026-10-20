using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The ER7 reader a conformance profile reads elements through: MSH's own
/// numbering, components and subcomponents, repetitions, repeated segments,
/// and the empty and absent cases a usage check turns on.
/// </summary>
public class Er7MessageTests
{
    private const string Message =
        "MSH|^~\\&#|APP|LAB^07D0092913^CLIA|CT^2.16.840.1.113883.3.5609.4.1.1.3.2.2^ISO|CTA-DPH|20151004||ORU^R01^ORU_R01|1|P|2.5.1\r"
        + "PID|1||A1^^^Lab&07D0092913&CLIA^MR~B2^^^Lab&2.16.840.1&ISO^PI||Doe^Jane\r"
        + "OBX|1|NM|5671-3^Lead^LN\r"
        + "OBX|2||10368-9^Lead^LN";

    [Fact]
    public void ParseRefusesTextThatDoesNotBeginWithMsh()
    {
        Assert.Throws<Hl7ParseException>(() => Er7Message.Parse("PID|1||A1"));
    }

    [Fact]
    public void ParseReadsTheSegmentsInOrderAcrossAnyLineEnding()
    {
        var message = Er7Message.Parse(Message.Replace("\r", "\r\n"));

        Assert.Equal(["MSH", "PID", "OBX", "OBX"], message.Segments.Select(segment => segment[..3]));
    }

    [Fact]
    public void ValuesNumbersMshFromTheFieldSeparator()
    {
        var message = Er7Message.Parse(Message);

        Assert.Equal(["|"], message.Values("MSH-1"));
        Assert.Equal(["^~\\&#"], message.Values("MSH-2"));
        Assert.Equal(["2.16.840.1.113883.3.5609.4.1.1.3.2.2"], message.Values("MSH-5.2"));
    }

    [Fact]
    public void ValuesReadsComponentsAndSubcomponentsOfEveryRepetition()
    {
        var message = Er7Message.Parse(Message);

        Assert.Equal(["Lab&07D0092913&CLIA", "Lab&2.16.840.1&ISO"], message.Values("PID-3.4"));
        Assert.Equal(["CLIA", "ISO"], message.Values("PID-3.4.3"));
    }

    [Fact]
    public void ValuesReadsEveryOccurrenceOfTheSegmentAndAnEmptyStringWhereOneIsEmpty()
    {
        var message = Er7Message.Parse(Message);

        Assert.Equal(["NM", ""], message.Values("OBX-2"));
        Assert.Equal([""], message.Values("PID-30"));
    }

    [Fact]
    public void ValuesReadsNothingForASegmentThatDoesNotOccurAndTheWholeTextForASegmentPath()
    {
        var message = Er7Message.Parse(Message);

        Assert.Empty(message.Values("NK1"));
        Assert.Empty(message.Values("NK1-2"));
        Assert.Equal(2, message.Values("OBX").Count);
    }
}
