using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the treatment endpoints decide without the database: the list-row wire
/// shape, which carries the treatment's columns and the count of conditions it
/// is listed for, and what a treatment to save must be in itself — its name and
/// that no condition is named twice. Whether each named condition exists is the
/// database's to answer and is proven in the integration suite.
/// </summary>
public class TreatmentEndpointsTests
{
    [Fact]
    public void ToResponseCarriesTheTreatmentsColumnsAndTheConditionCount()
    {
        var treatment = new Treatment
        {
            Id = 7,
            Name = "Doxycycline",
            Active = true,
            DefaultTreatment = true,
            CreatedAt = new DateTimeOffset(2026, 3, 1, 9, 30, 0, TimeSpan.Zero),
        };

        Assert.Equal(
            new TreatmentResponse(7, "Doxycycline", true, true, 3),
            TreatmentEndpoints.ToResponse.Compile().Invoke(treatment, 3));
    }

    [Fact]
    public void WhyNotATreatmentAcceptsANameAlone()
    {
        Assert.Null(TreatmentEndpoints.WhyNotATreatment(new SaveTreatmentRequest("Doxycycline")));
    }

    [Fact]
    public void WhyNotATreatmentAcceptsEveryFieldSet()
    {
        Assert.Null(TreatmentEndpoints.WhyNotATreatment(new SaveTreatmentRequest(
            "Doxycycline",
            Active: false,
            DefaultTreatment: true,
            ConditionIds: [4, 5, 6])));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void WhyNotATreatmentRefusesATreatmentWithNoName(string name)
    {
        Assert.Contains("name is required",
            TreatmentEndpoints.WhyNotATreatment(new SaveTreatmentRequest(name)),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotATreatmentRefusesANameLongerThanTheColumn()
    {
        Assert.Contains("name is longer than 200",
            TreatmentEndpoints.WhyNotATreatment(new SaveTreatmentRequest(new string('a', 201))),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotATreatmentRefusesADuplicateConditionId()
    {
        Assert.Contains("the same condition twice",
            TreatmentEndpoints.WhyNotATreatment(new SaveTreatmentRequest(
                "Doxycycline",
                ConditionIds: [4, 5, 4])),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotATreatmentRecordAcceptsATreatmentAlone()
    {
        Assert.Null(TreatmentEndpoints.WhyNotATreatmentRecord(new SaveTreatmentRecordRequest(7)));
    }

    [Fact]
    public void WhyNotATreatmentRecordAcceptsEveryFieldSet()
    {
        Assert.Null(TreatmentEndpoints.WhyNotATreatmentRecord(new SaveTreatmentRecordRequest(
            7,
            new DateOnly(2026, 3, 1),
            new DateOnly(2026, 3, 14),
            "100 mg twice daily",
            "Tolerated well")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WhyNotATreatmentRecordRefusesNoTreatmentChosen(int treatmentId)
    {
        Assert.Contains("treatmentId is required",
            TreatmentEndpoints.WhyNotATreatmentRecord(new SaveTreatmentRecordRequest(treatmentId)),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotATreatmentRecordRefusesAStopBeforeTheStart()
    {
        Assert.Contains("before treatmentDate",
            TreatmentEndpoints.WhyNotATreatmentRecord(new SaveTreatmentRecordRequest(
                7,
                new DateOnly(2026, 3, 14),
                new DateOnly(2026, 3, 1))),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotATreatmentRecordRefusesADoseLongerThanTheColumn()
    {
        Assert.Contains("dose is longer than 100",
            TreatmentEndpoints.WhyNotATreatmentRecord(
                new SaveTreatmentRecordRequest(7, Dose: new string('a', 101))),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotATreatmentRecordRefusesACommentLongerThanTheColumn()
    {
        Assert.Contains("comment is longer than 1000",
            TreatmentEndpoints.WhyNotATreatmentRecord(
                new SaveTreatmentRecordRequest(7, Comment: new string('a', 1_001))),
            StringComparison.Ordinal);
    }

    [Fact]
    public void NotAConditionNamesTheCondition()
    {
        Assert.Contains("conditionId 42", TreatmentEndpoints.NotACondition(42), StringComparison.Ordinal);
    }

    [Fact]
    public void CsvFieldsWriteTheConditionsAsIdsInOrderJoinedBySemicolons()
    {
        Assert.Equal(
            ["3", "Doxycycline", "yes", "no", "4;12;74"],
            TreatmentEndpoints.CsvFields(new TreatmentCsvRow(3, "Doxycycline", true, false, [74, 4, 12])));
    }

    [Fact]
    public void ATreatmentReadsBackFromItsCsvRow()
    {
        var treatment = new TreatmentCsvRow(3, "Doxycycline", true, true, [4, 74]);

        var (rows, refused) = ReferenceCsv.Read(
            ReferenceCsv.Write(TreatmentEndpoints.CsvColumns, [TreatmentEndpoints.CsvFields(treatment)]),
            TreatmentEndpoints.CsvColumns);

        Assert.Empty(refused);
        var (row, why) = TreatmentEndpoints.FromCsv(Assert.Single(rows));
        Assert.Null(why);
        Assert.Equal(TreatmentEndpoints.CsvFields(treatment), TreatmentEndpoints.CsvFields(row!));
    }

    [Fact]
    public void FromCsvReadsABlankIdAndNoConditionsAsATreatmentToAddForNone()
    {
        var (rows, _) = ReferenceCsv.Read(
            "id,name,active,default_treatment,condition_ids\r\n,Rest,yes,,\r\n", TreatmentEndpoints.CsvColumns);

        var (row, why) = TreatmentEndpoints.FromCsv(Assert.Single(rows));

        Assert.Null(why);
        Assert.Equal((0, "Rest", true, false), (row!.Id, row.Name, row.Active, row.DefaultTreatment));
        Assert.Empty(row.ConditionIds);
    }

    [Theory]
    [InlineData("x,Rest,yes,no,", "id 'x' is not a whole number.")]
    [InlineData("3,Rest,yes,sometimes,", "default_treatment 'sometimes' is not yes or no.")]
    [InlineData("3,Rest,yes,no,4;flu", "condition_ids 'flu' is not a whole number.")]
    public void FromCsvRefusesAFieldItCannotRead(string line, string expected)
    {
        var (rows, _) = ReferenceCsv.Read(
            $"id,name,active,default_treatment,condition_ids\r\n{line}\r\n", TreatmentEndpoints.CsvColumns);

        Assert.Equal((null, expected), TreatmentEndpoints.FromCsv(Assert.Single(rows)));
    }

    [Fact]
    public void ARowNamingAConditionTwiceIsRefusedAsARequestIs()
    {
        var request = TreatmentEndpoints.Request(new TreatmentCsvRow(3, "Rest", true, false, [4, 4]));

        Assert.Contains("names the same condition twice", TreatmentEndpoints.WhyNotATreatment(request));
    }
}
