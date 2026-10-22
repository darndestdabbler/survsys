using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The zip-to-agency rules: what a zip code reads as, and what it resolves to
/// against a mapping — one agency, none, several, or not a zip at all.
/// </summary>
public class ZipAgencyRulesTests
{
    private static readonly ZipCodeAgency[] Mapping =
    [
        new() { ZipCode = "84108", AgencyId = 5 },
        new() { ZipCode = "84321", AgencyId = 1 },
        new() { ZipCode = "84060", AgencyId = 9 },
        new() { ZipCode = "84060", AgencyId = 15 },
    ];

    [Theory]
    [InlineData("84108", "84108")]
    [InlineData(" 84108\t", "84108")]
    [InlineData("84108-1234", "84108")]
    [InlineData(" 84108-1234 ", "84108")]
    public void NormalizeReadsTheFiveDigitZip(string zip, string expected)
    {
        Assert.Equal(expected, ZipAgencyRules.Normalize(zip));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("8410")]
    [InlineData("841080")]
    [InlineData("8410A")]
    [InlineData("84108-12")]
    [InlineData("84108-ABCD")]
    [InlineData("84108 1234")]
    public void NormalizeIsNullForAValueThatIsNotAZip(string? zip)
    {
        Assert.Null(ZipAgencyRules.Normalize(zip));
    }

    [Fact]
    public void ResolveNamesTheOneAgencyAMappedZipBelongsTo()
    {
        Assert.Equal(
            new ZipResolution(ZipResolutionOutcome.Resolved, 5),
            ZipAgencyRules.Resolve("84108", Mapping));
    }

    [Theory]
    [InlineData("84108-1234")]
    [InlineData(" 84108 ")]
    public void ResolveNormalizesBeforeItLooksUp(string zip)
    {
        Assert.Equal(
            new ZipResolution(ZipResolutionOutcome.Resolved, 5),
            ZipAgencyRules.Resolve(zip, Mapping));
    }

    [Fact]
    public void ResolveIsUnmappedForAZipNoAgencyClaims()
    {
        Assert.Equal(
            new ZipResolution(ZipResolutionOutcome.Unmapped),
            ZipAgencyRules.Resolve("99999", Mapping));
    }

    [Fact]
    public void ResolveIsUnmappedAgainstAnEmptyMapping()
    {
        Assert.Equal(
            new ZipResolution(ZipResolutionOutcome.Unmapped),
            ZipAgencyRules.Resolve("84108", []));
    }

    /// <summary>
    /// A zip two agencies claim names neither, rather than whichever row came
    /// first.
    /// </summary>
    [Fact]
    public void ResolveIsAmbiguousForAZipTwoAgenciesClaim()
    {
        Assert.Equal(
            new ZipResolution(ZipResolutionOutcome.Ambiguous),
            ZipAgencyRules.Resolve("84060", Mapping));
    }

    /// <summary>
    /// The same agency listed twice for a zip is still one agency.
    /// </summary>
    [Fact]
    public void ResolveCountsAnAgencyListedTwiceOnce()
    {
        ZipCodeAgency[] mapping =
        [
            new() { ZipCode = "84321", AgencyId = 1 },
            new() { ZipCode = "84321", AgencyId = 1 },
        ];

        Assert.Equal(
            new ZipResolution(ZipResolutionOutcome.Resolved, 1),
            ZipAgencyRules.Resolve("84321", mapping));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ABCDE")]
    public void ResolveIsNotAZipForAValueThatIsNotOne(string? zip)
    {
        Assert.Equal(
            new ZipResolution(ZipResolutionOutcome.NotAZip),
            ZipAgencyRules.Resolve(zip, Mapping));
    }

    [Fact]
    public void ResolveRefusesAMissingMapping()
    {
        Assert.Throws<ArgumentNullException>(() => ZipAgencyRules.Resolve("84108", null!));
    }
}
