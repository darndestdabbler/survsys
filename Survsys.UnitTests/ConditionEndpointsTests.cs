using System.Linq.Expressions;
using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the conditions endpoint makes of a stored row, without a database and
/// without a host — what needs either is the integration suite's.
/// </summary>
/// <remarks>
/// The page bounds this endpoint answers by are no longer its own: they are
/// <c>Paging</c>'s, which every paged endpoint asks, and
/// <see cref="PagingTests"/> proves them there.
/// </remarks>
public class ConditionEndpointsTests
{
    [Fact]
    public void ToResponseCarriesTheFourWireFields()
    {
        var response = ConditionEndpoints.ToResponse.Compile().Invoke(new Condition
        {
            Id = 74,
            Name = "Hepatitis A",
            CdcCode = "10110",
            Active = true,
            ContactLeadIn = "Obtain information for all contacts exposed during the communicable period.",
            PlaceLeadIn = "Identify every place the case ate or worked.",
            TreatmentLeadIn = "Immune globulin within two weeks of exposure.",
            ConditionGroupId = 3,
        });

        Assert.Equal(new(74, "Hepatitis A", "10110", true), response);
    }

    [Fact]
    public void ToResponseCarriesAConditionWithNoCdcCode()
    {
        var response = ConditionEndpoints.ToResponse.Compile().Invoke(new Condition
        {
            Id = 9,
            Name = "Contact",
            CdcCode = null,
            Active = false,
        });

        Assert.Equal(new(9, "Contact", null, false), response);
    }

    /// <summary>
    /// The projection reads four columns and no others, which is what keeps
    /// the three lead-in columns — paragraphs of prose apiece — out of the
    /// <c>SELECT</c> list EF Core builds from it. Read off the expression
    /// tree, because that is the thing EF Core translates.
    /// </summary>
    [Fact]
    public void ToResponseReadsNoColumnItDoesNotPutOnTheWire()
    {
        var construction = Assert.IsAssignableFrom<NewExpression>(ConditionEndpoints.ToResponse.Body);

        var read = construction.Arguments
            .Cast<MemberExpression>()
            .Select(argument => argument.Member.Name)
            .ToList();

        Assert.Equal(
            [nameof(Condition.Id), nameof(Condition.Name), nameof(Condition.CdcCode), nameof(Condition.Active)],
            read);
    }

    private static Condition HepatitisA() => new()
    {
        Id = 74,
        Name = "Hepatitis A",
        CdcCode = "10110",
        Active = true,
        ContactLeadIn = "Obtain information for all contacts.",
        PlaceLeadIn = "Identify every place the case ate or worked.",
        TreatmentLeadIn = "Immune globulin within two weeks of exposure.",
        ConditionGroupId = 3,
    };

    private static readonly ConditionDetailResponse HepatitisADetail = new(
        74,
        "Hepatitis A",
        "10110",
        true,
        "Obtain information for all contacts.",
        "Identify every place the case ate or worked.",
        "Immune globulin within two weeks of exposure.");

    [Fact]
    public void ToDetailCarriesEveryColumnButTheGroup()
    {
        Assert.Equal(HepatitisADetail, ConditionEndpoints.ToDetail.Compile().Invoke(HepatitisA()));
    }

    /// <summary>
    /// The intake flags are read from the condition; the guide is another
    /// table's, so the mapping leaves it for the read to fill.
    /// </summary>
    [Fact]
    public void ToDetailCarriesTheIntakeFlagsAndLeavesTheGuide()
    {
        var condition = HepatitisA();
        condition.BypassOutOfState = true;
        condition.BlockPreliminary = true;

        var detail = ConditionEndpoints.Detail(condition);

        Assert.True(detail.BypassOutOfState);
        Assert.True(detail.BlockPreliminary);
        Assert.Null(detail.GuideId);
    }

    [Fact]
    public void DetailIsTheReadMappingItself()
    {
        Assert.Equal(HepatitisADetail, ConditionEndpoints.Detail(HepatitisA()));
    }

    [Fact]
    public void WhyNotAConditionAcceptsANameAndEveryOptionalField()
    {
        Assert.Null(ConditionEndpoints.WhyNotACondition(
            new SaveConditionRequest("Hepatitis A", true, "10110", "a", "b", "c")));
    }

    [Fact]
    public void WhyNotAConditionAcceptsAConditionWithOnlyAName()
    {
        Assert.Null(ConditionEndpoints.WhyNotACondition(new SaveConditionRequest("Contact", false)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void WhyNotAConditionRefusesAConditionWithNoName(string name)
    {
        Assert.Contains("name is required",
            ConditionEndpoints.WhyNotACondition(new SaveConditionRequest(name, true)),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAConditionRefusesANameLongerThanTheColumn()
    {
        Assert.Contains("name is longer than 200",
            ConditionEndpoints.WhyNotACondition(new SaveConditionRequest(new string('a', 201), true)),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAConditionRefusesACdcCodeLongerThanTheColumn()
    {
        Assert.Contains("cdcCode is longer than 20",
            ConditionEndpoints.WhyNotACondition(new SaveConditionRequest("Hepatitis A", true, new string('1', 21))),
            StringComparison.Ordinal);
    }

    [Fact]
    public void AConditionReadsBackFromItsCsvRowLeadInLineBreaksAndAll()
    {
        var condition = HepatitisADetail with { ContactLeadIn = "Name every contact.\r\nThen call each." };

        var (rows, refused) = ReferenceCsv.Read(
            ReferenceCsv.Write(ConditionEndpoints.CsvColumns, [ConditionEndpoints.CsvFields(condition)]),
            ConditionEndpoints.CsvColumns);

        Assert.Empty(refused);
        Assert.Equal((condition, null), ConditionEndpoints.FromCsv(Assert.Single(rows)));
    }

    [Fact]
    public void AConditionsIntakeFlagsReadBackFromItsCsvRow()
    {
        var condition = HepatitisADetail with { BypassOutOfState = true, BlockPreliminary = true };

        var (rows, _) = ReferenceCsv.Read(
            ReferenceCsv.Write(ConditionEndpoints.CsvColumns, [ConditionEndpoints.CsvFields(condition)]),
            ConditionEndpoints.CsvColumns);

        Assert.Equal((condition, null), ConditionEndpoints.FromCsv(Assert.Single(rows)));
    }

    [Fact]
    public void FromCsvRefusesAnIntakeFlagItCannotRead()
    {
        var (rows, _) = ReferenceCsv.Read(
            "id,name,active,block_preliminary\r\n74,Mpox,yes,sometimes\r\n", ["id", "name", "active"]);

        Assert.Equal(
            (null, "block_preliminary 'sometimes' is not yes or no."),
            ConditionEndpoints.FromCsv(Assert.Single(rows)));
    }

    [Fact]
    public void FromCsvReadsABlankIdAsAConditionToAdd()
    {
        var (rows, _) = ReferenceCsv.Read("id,name,active\r\n,Mpox,yes\r\n", ["id", "name", "active"]);

        Assert.Equal(
            (new ConditionDetailResponse(0, "Mpox", null, true, null, null, null), null),
            ConditionEndpoints.FromCsv(Assert.Single(rows)));
    }

    [Theory]
    [InlineData("x,Mpox,yes", "id 'x' is not a whole number.")]
    [InlineData("74,Mpox,perhaps", "active 'perhaps' is not yes or no.")]
    public void FromCsvRefusesAnIdOrActiveItCannotRead(string line, string expected)
    {
        var (rows, _) = ReferenceCsv.Read($"id,name,active\r\n{line}\r\n", ["id", "name", "active"]);

        Assert.Equal((null, expected), ConditionEndpoints.FromCsv(Assert.Single(rows)));
    }

    [Fact]
    public void RequestCarriesTheRowsValues()
    {
        Assert.Equal(
            new SaveConditionRequest(
                "Hepatitis A",
                true,
                "10110",
                "Obtain information for all contacts.",
                "Identify every place the case ate or worked.",
                "Immune globulin within two weeks of exposure."),
            ConditionEndpoints.Request(HepatitisADetail));
    }

    [Fact]
    public void RequestCarriesTheGuideAndTheIntakeFlags()
    {
        var request = ConditionEndpoints.Request(
            HepatitisADetail with { GuideId = 5, BypassOutOfState = true, BlockPreliminary = true });

        Assert.Equal((5, true, true), (request.GuideId, request.BypassOutOfState, request.BlockPreliminary));
    }

    [Fact]
    public void WhyNotLabOverridesAcceptsAnOrganismAndATestType()
    {
        Assert.Null(ConditionEndpoints.WhyNotLabOverrides(
            [new ConditionLabOverrideRow("organism", 4, true), new ConditionLabOverrideRow("test_type", 4, false)]));
        Assert.Null(ConditionEndpoints.WhyNotLabOverrides([]));
    }

    [Fact]
    public void WhyNotLabOverridesRefusesAnUnknownKindNoItemOrAnItemTwice()
    {
        Assert.Contains("kind", ConditionEndpoints.WhyNotLabOverrides([new ConditionLabOverrideRow("treatment", 4, true)]));
        Assert.Contains("itemId", ConditionEndpoints.WhyNotLabOverrides([new ConditionLabOverrideRow("organism", 0, true)]));
        Assert.Contains("twice", ConditionEndpoints.WhyNotLabOverrides(
            [new ConditionLabOverrideRow("organism", 4, true), new ConditionLabOverrideRow("organism", 4, false)]));
    }
}
