using Survsys.Web.Components;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The Attachments table's date is the local day a file was attached, not
/// the day of the stored UTC moment.
/// </summary>
public class AttachmentsPanelTests
{
    [Fact]
    public void AttachedWritesTheLocalDayOfALateEveningMoment()
    {
        var evening = new DateTime(2026, 10, 22, 23, 30, 0, DateTimeKind.Local);
        var stored = new DateTimeOffset(evening).ToUniversalTime();

        Assert.Equal("2026-10-22", AttachmentsPanel.Attached(stored));
    }
}
