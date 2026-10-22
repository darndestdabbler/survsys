using Survsys.Api;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The batch notification of assigned messages: which agencies are sent one,
/// to whom, and what it lists. Pure throughout — no database or mail server.
/// </summary>
public class MessageNotificationServiceTests
{
    private static readonly DateTime Morning = new(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ComposeSendsEachAgencyWithAddressesOneBatchOfItsMessages()
    {
        var notifications = MessageNotificationService.Compose(
            [
                new AssignedMessage(3, 5, "202600003", "Chlamydia", Morning.AddHours(2)),
                new AssignedMessage(1, 5, "202600001", "Gonorrhea", Morning),
                new AssignedMessage(2, 7, "202600002", "Chlamydia", Morning.AddHours(1)),
            ],
            new Dictionary<int, string?> { [5] = "epi@example.org; lab@example.org", [7] = "slco@example.org" });

        Assert.Equal([5, 7], notifications.Select(notification => notification.AgencyId));

        var first = notifications[0];
        Assert.Equal(["epi@example.org", "lab@example.org"], first.To);
        Assert.Equal([1, 3], first.MessageIds);
        Assert.Contains("2 laboratory message(s)", first.Subject, StringComparison.Ordinal);
        Assert.True(
            first.Body.IndexOf("202600001", StringComparison.Ordinal)
                < first.Body.IndexOf("202600003", StringComparison.Ordinal));
        Assert.Contains("Gonorrhea", first.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void ComposeSendsNothingToAnAgencyWithNoAddressesOrForACaseWithNoAgency()
    {
        var notifications = MessageNotificationService.Compose(
            [
                new AssignedMessage(1, 5, "202600001", "Chlamydia", Morning),
                new AssignedMessage(2, 9, "202600002", "Chlamydia", Morning),
                new AssignedMessage(3, null, "202600003", "Chlamydia", Morning),
            ],
            new Dictionary<int, string?> { [5] = null, [9] = " ; " });

        Assert.Empty(notifications);
    }

    [Fact]
    public void ComposeSendsNothingWhenNothingWasAssigned()
    {
        Assert.Empty(MessageNotificationService.Compose([], new Dictionary<int, string?> { [5] = "epi@example.org" }));
    }

    [Theory]
    [InlineData("epi@example.org;lab@example.org", new[] { "epi@example.org", "lab@example.org" })]
    [InlineData(" epi@example.org , ", new[] { "epi@example.org" })]
    [InlineData(null, new string[0])]
    public void AddressesSplitsTheStoredListAndDropsBlanks(string? stored, string[] expected)
    {
        Assert.Equal(expected, MessageNotificationService.Addresses(stored));
    }

    [Theory]
    [InlineData(10, "smtp.example.org", "survsys@example.org", true)]
    [InlineData(null, "smtp.example.org", "survsys@example.org", false)]
    [InlineData(10, "", "survsys@example.org", false)]
    [InlineData(10, "smtp.example.org", " ", false)]
    public void IsConfiguredNeedsAnIntervalAHostAndASender(int? interval, string host, string from, bool configured)
    {
        var options = new MessageNotificationOptions { IntervalMinutes = interval, Host = host, From = from };

        Assert.Equal(configured, options.IsConfigured);
    }
}
