using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the vaccine endpoints decide without the database: the catalogue-row and
/// record wire shapes, and what a record to save must be in itself — a vaccine
/// chosen, a dose of one or more, an expiration not before the administered
/// date, and a lot number and comment within bounds. Whether the vaccine and the
/// case exist is the database's to answer and is proven in the integration suite.
/// </summary>
public class VaccineEndpointsTests
{
    [Fact]
    public void ToResponseCarriesTheVaccinesCatalogueColumns()
    {
        var vaccine = new Vaccine { Id = 15, Code = "03", Description = "MMR", SortOrder = 15, Active = false };

        Assert.Equal(
            new VaccineResponse(15, "03", "MMR", 15, false),
            VaccineEndpoints.ToResponse.Compile().Invoke(vaccine));
    }

    [Fact]
    public void WhyNotAVaccineAcceptsACodeAlone()
    {
        Assert.Null(VaccineEndpoints.WhyNotAVaccine(new SaveVaccineRequest("03")));
    }

    [Theory]
    [InlineData(" ", null, 0, "code is required")]
    [InlineData("123456789012345678901", null, 0, "code is longer than 20")]
    [InlineData("03", null, -1, "sortOrder -1 is negative")]
    public void WhyNotAVaccineRefusesABadCodeOrSortOrder(
        string code, string? description, int sortOrder, string expected)
    {
        Assert.Contains(
            expected,
            VaccineEndpoints.WhyNotAVaccine(new SaveVaccineRequest(code, description, sortOrder)));
    }

    [Fact]
    public void WhyNotAVaccineRefusesADescriptionOverTwoHundredCharacters()
    {
        Assert.Contains(
            "description is longer than 200",
            VaccineEndpoints.WhyNotAVaccine(new SaveVaccineRequest("03", new string('x', 201))));
    }

    [Fact]
    public void AVaccineReadsBackFromItsCsvRow()
    {
        var vaccine = new SaveVaccineRequest("03", "MMR", 15, false);

        var (rows, refused) = ReferenceCsv.Read(
            ReferenceCsv.Write(VaccineEndpoints.CsvColumns, [VaccineEndpoints.CsvFields(vaccine)]),
            VaccineEndpoints.CsvColumns);

        Assert.Empty(refused);
        Assert.Equal((vaccine, null), VaccineEndpoints.FromCsv(Assert.Single(rows)));
    }

    [Fact]
    public void FromCsvReadsABlankSortOrderAsZero()
    {
        var (rows, _) = ReferenceCsv.Read("code,description,sort_order,active\r\n03,MMR,,yes\r\n", VaccineEndpoints.CsvColumns);

        Assert.Equal(
            (new SaveVaccineRequest("03", "MMR", 0, true), null),
            VaccineEndpoints.FromCsv(Assert.Single(rows)));
    }

    [Theory]
    [InlineData("03,MMR,first,yes", "sort_order 'first' is not a whole number")]
    [InlineData("03,MMR,1,maybe", "active 'maybe' is not yes or no")]
    public void FromCsvRefusesASortOrderOrActiveItCannotRead(string line, string expected)
    {
        var (rows, _) = ReferenceCsv.Read($"code,description,sort_order,active\r\n{line}\r\n", VaccineEndpoints.CsvColumns);

        var (row, why) = VaccineEndpoints.FromCsv(Assert.Single(rows));

        Assert.Null(row);
        Assert.Equal($"{expected}.", why);
    }

    [Fact]
    public void RecordToResponseCarriesTheRecordAndTheVaccineDescription()
    {
        var record = new PersonVaccine
        {
            Id = 7,
            PersonConditionId = 3,
            VaccineId = 15,
            AdministeredDate = new DateOnly(2026, 3, 1),
            DoseNumber = 2,
            LotNumber = "ABC123",
            ExpirationDate = new DateOnly(2027, 3, 1),
            Comment = "No reaction",
            Manufacturer = "MSD",
            InformationSource = "PHC1435",
            CreatedAt = new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero),
        };

        Assert.Equal(
            new VaccineRecordResponse(
                7,
                15,
                "MMR",
                new DateOnly(2026, 3, 1),
                2,
                "ABC123",
                new DateOnly(2027, 3, 1),
                "No reaction",
                "MSD",
                "PHC1435"),
            VaccineEndpoints.RecordToResponse.Compile().Invoke(record, "MMR"));
    }

    [Fact]
    public void WhyNotAVaccineRecordAcceptsAVaccineAlone()
    {
        Assert.Null(VaccineEndpoints.WhyNotAVaccineRecord(new SaveVaccineRecordRequest(15)));
    }

    [Fact]
    public void WhyNotAVaccineRecordAcceptsEveryFieldSet()
    {
        Assert.Null(VaccineEndpoints.WhyNotAVaccineRecord(new SaveVaccineRecordRequest(
            15,
            new DateOnly(2026, 3, 1),
            DoseNumber: 2,
            LotNumber: "ABC123",
            ExpirationDate: new DateOnly(2027, 3, 1),
            Comment: "No reaction")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WhyNotAVaccineRecordRefusesNoVaccineChosen(int vaccineId)
    {
        Assert.Contains("vaccineId is required",
            VaccineEndpoints.WhyNotAVaccineRecord(new SaveVaccineRecordRequest(vaccineId)),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void WhyNotAVaccineRecordRefusesADoseBelowOne(int dose)
    {
        Assert.Contains("is not a dose",
            VaccineEndpoints.WhyNotAVaccineRecord(new SaveVaccineRecordRequest(15, DoseNumber: dose)),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAVaccineRecordRefusesAnExpirationBeforeTheAdministeredDate()
    {
        Assert.Contains("before administeredDate",
            VaccineEndpoints.WhyNotAVaccineRecord(new SaveVaccineRecordRequest(
                15,
                new DateOnly(2026, 3, 1),
                ExpirationDate: new DateOnly(2026, 2, 1))),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAVaccineRecordRefusesAManufacturerLongerThanTheColumn()
    {
        Assert.Contains("manufacturer is longer than 50",
            VaccineEndpoints.WhyNotAVaccineRecord(
                new SaveVaccineRecordRequest(15, Manufacturer: new string('a', 51))),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAVaccineRecordRefusesAnInformationSourceLongerThanTheColumn()
    {
        Assert.Contains("informationSource is longer than 50",
            VaccineEndpoints.WhyNotAVaccineRecord(
                new SaveVaccineRecordRequest(15, InformationSource: new string('a', 51))),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAVaccineRecordRefusesALotNumberLongerThanTheColumn()
    {
        Assert.Contains("lotNumber is longer than 100",
            VaccineEndpoints.WhyNotAVaccineRecord(
                new SaveVaccineRecordRequest(15, LotNumber: new string('a', 101))),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAVaccineRecordRefusesACommentLongerThanTheGuard()
    {
        Assert.Contains("comment is longer than",
            VaccineEndpoints.WhyNotAVaccineRecord(
                new SaveVaccineRecordRequest(15, Comment: new string('a', VaccineEndpoints.CommentLength + 1))),
            StringComparison.Ordinal);
    }
}
