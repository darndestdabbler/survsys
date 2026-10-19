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
}
