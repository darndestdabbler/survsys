using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the access audit endpoint decides without the database: the wire
/// shape of a record.
/// </summary>
public class AccessAuditEndpointsTests
{
    [Fact]
    public void ToResponseCarriesTheRecordAndTheUserName()
    {
        var accessedAt = new DateTime(2026, 10, 16, 20, 5, 0, DateTimeKind.Utc);
        var record = new AccessRecord { Id = 9, UserId = 4, PersonConditionId = 12, AccessedAt = accessedAt };

        Assert.Equal(
            new AccessRecordResponse(9, 4, "ada", 12, accessedAt),
            AccessAuditEndpoints.ToResponse(record, "ada"));
    }
}
