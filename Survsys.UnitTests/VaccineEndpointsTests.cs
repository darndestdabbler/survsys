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
        var vaccine = new Vaccine { Id = 15, Code = "03", Description = "MMR", SortOrder = 15 };

        Assert.Equal(
            new VaccineResponse(15, "03", "MMR"),
            VaccineEndpoints.ToResponse.Compile().Invoke(vaccine));
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
