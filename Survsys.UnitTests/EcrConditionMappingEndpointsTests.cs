using Survsys.Api;
using Survsys.Contracts;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the eCR condition mapping endpoints decide without the database: what
/// a mapping to save must be in itself, and its CSV row read and written.
/// Whether the code is taken and the condition is on file is the database's to
/// answer and is proven in the integration suite.
/// </summary>
public class EcrConditionMappingEndpointsTests
{
    private const string Snomed = "2.16.840.1.113883.6.96";

    [Fact]
    public void WhyNotAMappingAcceptsACodeSystemAndCodeAlone()
    {
        Assert.Null(EcrConditionMappingEndpoints.WhyNotAMapping(new SaveEcrConditionMappingRequest(Snomed, "3928002")));
    }

    [Theory]
    [InlineData(" ", "3928002", null, "codeSystem is required")]
    [InlineData(Snomed, "", null, "code is required")]
    [InlineData(Snomed, "123456789012345678901234567890123456789012345678901", null, "code is longer than 50")]
    public void WhyNotAMappingRefusesAMissingOrLongCode(
        string codeSystem, string code, string? displayName, string expected)
    {
        Assert.Contains(
            expected,
            EcrConditionMappingEndpoints.WhyNotAMapping(new SaveEcrConditionMappingRequest(codeSystem, code, displayName)));
    }

    [Fact]
    public void WhyNotAMappingRefusesALongCodeSystemOrDisplayName()
    {
        Assert.Contains(
            "codeSystem is longer than 100",
            EcrConditionMappingEndpoints.WhyNotAMapping(new SaveEcrConditionMappingRequest(new string('1', 101), "1")));
        Assert.Contains(
            "displayName is longer than 500",
            EcrConditionMappingEndpoints.WhyNotAMapping(
                new EcrConditionMappingCsvRow(Snomed, "1", new string('x', 501), null)));
    }

    [Fact]
    public void AMappingReadsBackFromItsCsvRow()
    {
        var mapping = new EcrConditionMappingCsvRow(Snomed, "3928002", "Zika virus disease (disorder)", "Zika virus");

        var (rows, refused) = ReferenceCsv.Read(
            ReferenceCsv.Write(EcrConditionMappingEndpoints.CsvColumns, [EcrConditionMappingEndpoints.CsvFields(mapping)]),
            EcrConditionMappingEndpoints.CsvColumns);

        Assert.Empty(refused);
        Assert.Equal(mapping, EcrConditionMappingEndpoints.FromCsv(Assert.Single(rows)));
    }

    [Fact]
    public void FromCsvReadsABlankConditionAsUnmapped()
    {
        var (rows, _) = ReferenceCsv.Read(
            $"code_system,code,display_name,condition\r\n {Snomed} , 3928002 ,,\r\n",
            EcrConditionMappingEndpoints.CsvColumns);

        Assert.Equal(
            new EcrConditionMappingCsvRow(Snomed, "3928002", null, null),
            EcrConditionMappingEndpoints.FromCsv(Assert.Single(rows)));
    }
}
