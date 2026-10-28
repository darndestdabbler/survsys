using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the provider endpoints decide without the database: the wire shape,
/// what a provider to save must be, and its CSV form.
/// </summary>
public class ProviderEndpointsTests
{
    private static Provider Stored() => new()
    {
        Id = 7,
        ProviderTypeId = 1175,
        FirstName = "Ada",
        MiddleName = "M",
        LastName = "Lovelace",
        Phone = "860-555-0123",
        Email = "ada@example.org",
        Active = false,
    };

    private static readonly ProviderResponse Expected = new(
        7, 1175, "Ada", "M", "Lovelace", "860-555-0123", "ada@example.org", false);

    private static SaveProviderRequest Valid() => new(
        "Lovelace", "Ada", "M", 1175, "860-555-0123", "ada@example.org", false);

    [Fact]
    public void ToResponseCarriesEveryColumn()
    {
        Assert.Equal(Expected, ProviderEndpoints.ToResponse.Compile().Invoke(Stored()));
    }

    [Fact]
    public void RespondIsTheReadMappingItself()
    {
        Assert.Equal(Expected, ProviderEndpoints.Respond(Stored()));
    }

    [Fact]
    public void WhyNotAProviderAcceptsAProviderWithEveryField()
    {
        Assert.Null(ProviderEndpoints.WhyNotAProvider(Valid()));
    }

    [Fact]
    public void WhyNotAProviderAcceptsAProviderWithOnlyALastName()
    {
        Assert.Null(ProviderEndpoints.WhyNotAProvider(new SaveProviderRequest("Lovelace")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void WhyNotAProviderRefusesAProviderWithNoLastName(string lastName)
    {
        Assert.Contains("lastName is required",
            ProviderEndpoints.WhyNotAProvider(Valid() with { LastName = lastName }),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAProviderRefusesAValueLongerThanItsColumn()
    {
        Assert.Contains("firstName is longer than 100",
            ProviderEndpoints.WhyNotAProvider(Valid() with { FirstName = new string('a', 101) }),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAProviderRefusesAnEmailWithNoAt()
    {
        Assert.Contains("email 'ada.example.org'",
            ProviderEndpoints.WhyNotAProvider(Valid() with { Email = "ada.example.org" }),
            StringComparison.Ordinal);
    }

    [Fact]
    public void NotAProviderTypeNamesTheIdAndTheList()
    {
        var why = ProviderEndpoints.NotAProviderType(42);

        Assert.Contains("providerTypeId 42", why, StringComparison.Ordinal);
        Assert.Contains("/api/codes/provider_type", why, StringComparison.Ordinal);
    }

    [Fact]
    public void AProviderReadsBackFromItsCsvRow()
    {
        var (rows, refused) = ReferenceCsv.Read(
            ReferenceCsv.Write(ProviderEndpoints.CsvColumns, [ProviderEndpoints.CsvFields(Expected)]),
            ProviderEndpoints.CsvColumns);

        Assert.Empty(refused);
        Assert.Equal((Expected, null), ProviderEndpoints.FromCsv(Assert.Single(rows)));
    }

    [Fact]
    public void FromCsvReadsABlankIdAsAProviderToAddAndABlankActiveAsActive()
    {
        var (rows, _) = ReferenceCsv.Read("id,last_name,active\r\n,Hopper,\r\n", ["id", "last_name", "active"]);

        var (row, why) = ProviderEndpoints.FromCsv(Assert.Single(rows));

        Assert.Null(why);
        Assert.Equal(0, row!.Id);
        Assert.Equal("Hopper", row.LastName);
        Assert.True(row.Active);
        Assert.Empty(ProviderEndpoints.CsvFields(row)[0] ?? "");
    }

    [Theory]
    [InlineData("x,,Hopper,yes", "id 'x' is not a whole number.")]
    [InlineData(",y,Hopper,yes", "provider_type_id 'y' is not a whole number.")]
    [InlineData(",,Hopper,maybe", "active 'maybe' is not yes or no.")]
    public void FromCsvRefusesAFieldItCannotRead(string line, string expected)
    {
        string[] columns = ["id", "provider_type_id", "last_name", "active"];
        var (rows, _) = ReferenceCsv.Read($"{string.Join(',', columns)}\r\n{line}\r\n", columns);

        Assert.Equal((null, expected), ProviderEndpoints.FromCsv(Assert.Single(rows)));
    }

    [Fact]
    public void RequestCarriesTheRowsValues()
    {
        Assert.Equal(Valid(), ProviderEndpoints.Request(Expected));
    }
}
