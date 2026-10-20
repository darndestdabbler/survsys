using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// Who holds a case's edit lock: the user who took it, until it times out.
/// </summary>
public class EditLockRulesTests
{
    private static readonly DateTimeOffset Taken = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A case nobody locked is held by nobody.</summary>
    [Fact]
    public void HolderAtIsNobodyWhereNoLockWasTaken()
    {
        Assert.Null(EditLockRules.HolderAt(null, null, Taken));
    }

    /// <summary>A lock inside its timeout is held by the user who took it.</summary>
    [Fact]
    public void HolderAtIsTheUserWhileTheLockIsLive()
    {
        Assert.Equal(7, EditLockRules.HolderAt(7, Taken, Taken.AddMinutes(29)));
    }

    /// <summary>A lock exactly as old as the timeout has lapsed.</summary>
    [Fact]
    public void HolderAtIsNobodyAtTheTimeout()
    {
        Assert.Null(EditLockRules.HolderAt(7, Taken, Taken + EditLockRules.Timeout));
    }

    /// <summary>A lock older than the timeout has lapsed.</summary>
    [Fact]
    public void HolderAtIsNobodyPastTheTimeout()
    {
        Assert.Null(EditLockRules.HolderAt(7, Taken, Taken.AddHours(2)));
    }
}
