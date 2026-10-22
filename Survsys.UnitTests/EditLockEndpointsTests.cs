using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The edit lock's pure pieces: the lock as the wire carries it, and the
/// sentence a refusal gives.
/// </summary>
public class EditLockEndpointsTests
{
    private static readonly DateTimeOffset Taken = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A live holder is carried with when the lock lapses.</summary>
    [Fact]
    public void LockOfCarriesTheHolderAndWhenTheLockLapses()
    {
        Assert.Equal(
            new EditLockResponse(7, "Ada Investigator", Taken, Taken + EditLockRules.Timeout),
            EditLockEndpoints.LockOf(7, "Ada Investigator", Taken));
    }

    /// <summary>No live holder is an empty lock, whatever the columns still hold.</summary>
    [Fact]
    public void LockOfIsEmptyWhereNobodyHoldsTheLock()
    {
        Assert.Equal(
            new EditLockResponse(null, null, null, null),
            EditLockEndpoints.LockOf(null, "Ada Investigator", Taken));
    }

    /// <summary>A refusal names the holder and when the lock lapses, in UTC.</summary>
    [Fact]
    public void HeldByNamesTheHolderAndWhenTheLockLapses()
    {
        var why = EditLockEndpoints.HeldBy(EditLockEndpoints.LockOf(7, "Ada Investigator", Taken));

        Assert.Equal("This case is being edited by Ada Investigator until 2026-10-19 09:30 UTC.", why);
    }

    /// <summary>
    /// A lock that lapsed between the refusal and the read still gives a
    /// sentence rather than an empty name.
    /// </summary>
    [Fact]
    public void HeldByStillSaysSomethingOfALockThatHasLapsed()
    {
        Assert.Contains("someone else", EditLockEndpoints.HeldBy(new EditLockResponse(null, null, null, null)),
            StringComparison.Ordinal);
    }
}
