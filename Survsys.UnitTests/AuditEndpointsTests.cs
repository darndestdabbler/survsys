using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the change audit endpoint decides without the database: the wire shape
/// of an entry and the table it insists on.
/// </summary>
public class AuditEndpointsTests
{
    [Fact]
    public void ToResponseCarriesTheEntryAndTheUserName()
    {
        var occurredAt = new DateTime(2026, 10, 21, 20, 5, 0, DateTimeKind.Utc);
        var entry = new AuditEntry
        {
            Id = 3,
            TableName = "treatments",
            KeyValues = "8",
            Operation = "Update",
            OccurredAt = occurredAt,
            UserId = 4,
            OldValues = "{\"active\":true}",
            NewValues = "{\"active\":false}",
        };

        Assert.Equal(
            new AuditEntryResponse(3, "treatments", "8", "Update", occurredAt, 4, "ada", "{\"active\":true}", "{\"active\":false}"),
            AuditEndpoints.ToResponse(entry, "ada"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ARequestWithoutATableIsNotAnAuditRequest(string? table)
    {
        Assert.NotNull(AuditEndpoints.WhyNotAnAuditRequest(table));
    }

    [Fact]
    public void ARequestWithATableIsAnAuditRequest()
    {
        Assert.Null(AuditEndpoints.WhyNotAnAuditRequest("person_conditions"));
    }
}
