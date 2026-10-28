using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the eCR sender endpoints decide without the database: what a sender
/// to save must be in itself. Whether its id is taken is the database's to
/// answer and is proven in the integration suite.
/// </summary>
public class EcrSenderEndpointsTests
{
    private const string Custodian = "2.16.840.1.113883.4.6^88888888";

    [Theory]
    [InlineData(EcrSender.TestStage)]
    [InlineData(EcrSender.ValidationStage)]
    [InlineData(EcrSender.ProductionStage)]
    public void WhyNotASenderAcceptsAnIdANameAndEachStage(string stage)
    {
        Assert.Null(EcrSenderEndpoints.WhyNotASender(new SaveEcrSenderRequest(Custodian, "Salem Medical Center", stage)));
    }

    [Theory]
    [InlineData(" ", "Salem", EcrSender.TestStage, "senderId is required")]
    [InlineData(Custodian, "", EcrSender.TestStage, "name is required")]
    [InlineData(Custodian, "Salem", "live", "stage must be one of test, validation, production")]
    [InlineData(Custodian, "Salem", "Production", "stage must be one of")]
    public void WhyNotASenderRefusesAMissingIdOrNameOrAnUnknownStage(
        string senderId, string name, string stage, string expected)
    {
        Assert.Contains(expected, EcrSenderEndpoints.WhyNotASender(new SaveEcrSenderRequest(senderId, name, stage)));
    }

    [Fact]
    public void WhyNotASenderRefusesALongIdOrName()
    {
        Assert.Contains(
            "senderId is longer than 300",
            EcrSenderEndpoints.WhyNotASender(new SaveEcrSenderRequest(new string('1', 301), "Salem", EcrSender.TestStage)));
        Assert.Contains(
            "name is longer than 200",
            EcrSenderEndpoints.WhyNotASender(new SaveEcrSenderRequest(Custodian, new string('a', 201), EcrSender.TestStage)));
    }
}
