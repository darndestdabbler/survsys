using Survsys.Api;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// MLLP's framing and the ACK and NAK a framed message is answered with,
/// without a socket: that the listener carries them over TCP is
/// <c>Survsys.IntegrationTests</c>'s to prove.
/// </summary>
public class MllpFramingTests
{
    private const string Message =
        "MSH|^~\\&|LAB_SYSTEM|TEST_LAB^2.16.840.1.113883.3.72.5.30.1^ISO|EMSA|UTAH_DPH|20260312120000||ORU^R01^ORU_R01|MSG00001|P|2.5.1\r"
        + "PID|1||TESTPAT001";

    [Fact]
    public void FrameWrapsTheMessageInStartAndEndBlocks()
    {
        Assert.Equal("\u000b" + Message + "\u001c\r", MllpFraming.Frame(Message));
    }

    [Fact]
    public void TryUnframeTakesBackWhatFrameWrapped()
    {
        Assert.True(MllpFraming.TryUnframe(MllpFraming.Frame(Message), out var message, out var remainder));

        Assert.Equal(Message, message);
        Assert.Equal(string.Empty, remainder);
    }

    [Fact]
    public void TryUnframeWaitsForTheEndOfAPartialFrame()
    {
        var partial = MllpFraming.Frame(Message)[..20];

        Assert.False(MllpFraming.TryUnframe(partial, out _, out var remainder));
        Assert.Equal(partial, remainder);
    }

    [Fact]
    public void TryUnframeTakesOneFrameAtATimeAndDropsWhatPrecedesTheStartBlock()
    {
        var buffer = "noise" + MllpFraming.Frame("first") + MllpFraming.Frame("second")[..4];

        Assert.True(MllpFraming.TryUnframe(buffer, out var first, out var remainder));
        Assert.Equal("first", first);
        Assert.False(MllpFraming.TryUnframe(remainder, out _, out _));
    }

    [Fact]
    public void AcknowledgementAcceptsWithTheMessagesControlIdAndSwapsSenderAndReceiver()
    {
        var segments = MllpFraming.Acknowledgement(Message, MllpFraming.Accept).Split('\r');

        var msh = segments[0].Split('|');
        Assert.Equal("EMSA", msh[2]);
        Assert.Equal("UTAH_DPH", msh[3]);
        Assert.Equal("LAB_SYSTEM", msh[4]);
        Assert.Equal("TEST_LAB^2.16.840.1.113883.3.72.5.30.1^ISO", msh[5]);
        Assert.Equal("ACK^R01^ACK", msh[8]);
        Assert.Equal("2.5.1", msh[11]);
        Assert.Equal("MSA|AA|MSG00001", segments[1]);
    }

    [Fact]
    public void AcknowledgementCarriesAnEscapedReasonOnANak()
    {
        var msa = MllpFraming.Acknowledgement(Message, MllpFraming.Error, "sender A|B^C not on file")
            .Split('\r')[1];

        Assert.Equal("MSA|AE|MSG00001|sender A\\F\\B\\S\\C not on file", msa);
    }

    [Fact]
    public void AcknowledgementRejectsAMessageWithNoReadableHeader()
    {
        var segments = MllpFraming.Acknowledgement("not hl7", MllpFraming.Reject).Split('\r');

        Assert.StartsWith("MSH|^~\\&|", segments[0]);
        Assert.Equal("MSA|AR|", segments[1]);
    }
}
