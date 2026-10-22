using Survsys.Api;
using Survsys.Contracts;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the system-setting endpoints decide without the database: what a
/// setting to save must be.
/// </summary>
public class SystemSettingEndpointsTests
{
    [Fact]
    public void WhyNotASystemSettingAcceptsANameAndAValue()
    {
        Assert.Null(SystemSettingEndpoints.WhyNotASystemSetting(
            "cdc_batch_file_prefix", new SaveSystemSettingRequest("CT", "CDC batch file naming")));
    }

    /// <summary>An empty value sets the setting to nothing, which is a value.</summary>
    [Fact]
    public void WhyNotASystemSettingAcceptsAnEmptyValue()
    {
        Assert.Null(SystemSettingEndpoints.WhyNotASystemSetting("export_hold_default", new SaveSystemSettingRequest("")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void WhyNotASystemSettingRefusesASettingWithNoName(string? name)
    {
        Assert.Contains("name is required",
            SystemSettingEndpoints.WhyNotASystemSetting(name, new SaveSystemSettingRequest("CT")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotASystemSettingRefusesANameLongerThanTheColumn()
    {
        Assert.Contains("name is longer than 100",
            SystemSettingEndpoints.WhyNotASystemSetting(new string('a', 101), new SaveSystemSettingRequest("CT")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotASystemSettingRefusesNoValue()
    {
        Assert.Contains("value is required",
            SystemSettingEndpoints.WhyNotASystemSetting("cdc_batch_file_prefix", new SaveSystemSettingRequest(null!)),
            StringComparison.Ordinal);
        Assert.Contains("value is required",
            SystemSettingEndpoints.WhyNotASystemSetting("cdc_batch_file_prefix", null),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotASystemSettingRefusesADescriptionLongerThanTheColumn()
    {
        Assert.Contains("description is longer than 500",
            SystemSettingEndpoints.WhyNotASystemSetting(
                "cdc_batch_file_prefix", new SaveSystemSettingRequest("CT", new string('d', 501))),
            StringComparison.Ordinal);
    }
}
