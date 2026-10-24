using Survsys.Web.Components;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The Notes table's date is the local day a note was written, not the day
/// of the stored UTC moment.
/// </summary>
public class NotesTabTests
{
    [Fact]
    public void WrittenWritesTheLocalDayOfALateEveningMoment()
    {
        var evening = new DateTime(2026, 10, 22, 23, 30, 0, DateTimeKind.Local);
        var stored = new DateTimeOffset(evening).ToUniversalTime();

        Assert.Equal("2026-10-22", NotesTab.Written(stored));
    }
}
