using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the laboratory endpoints make of a stored specimen, its tests, their
/// results and its susceptibility, and what they refuse before asking the
/// database anything.
/// </summary>
/// <remarks>
/// No database and no host: that a specimen records and reads back whole, that
/// it links to its report, and that a missing report is a 400 and a missing
/// specimen a 404 are <c>Survsys.IntegrationTests</c>'s to prove against a
/// real SQL Server. The values here are the session-011 lab the snapshot
/// dumps: specimen <c>LAB-2026-011</c>, collected on the 8th of March, from
/// source 12, at facility 11.
/// </remarks>
public class LabEndpointsTests
{
    private static Lab Specimen() => new()
    {
        Id = 11,
        PersonId = 14,
        PersonConditionId = 14,
        LabFacilityId = 11,
        ProviderId = 13,
        DiagnosticFacilityId = 17,
        ReportingFacilityId = 18,
        SpecimenSourceId = 12,
        CollectionDate = new DateOnly(2026, 3, 8),
        AccessionNo = "LAB-2026-011",
        SpecimenSentToState = null,
    };

    [Fact]
    public void LabToResponseCarriesTheSpecimensColumns()
    {
        var response = LabEndpoints.LabToResponse.Compile().Invoke(Specimen());

        Assert.Equal(
            new LabResponse(
                11, 14, 14, 11, 13, 17, 18, 12, new DateOnly(2026, 3, 8), "LAB-2026-011", null),
            response);
    }

    /// <summary>
    /// A specimen with no facility, source, date, accession or sent flag: every
    /// one of those columns is nullable, and a specimen that has none comes
    /// back with nulls rather than with zeros or a default date.
    /// </summary>
    [Fact]
    public void LabToResponseCarriesABareSpecimen()
    {
        var bare = new Lab { Id = 20, PersonId = 14 };

        var response = LabEndpoints.LabToResponse.Compile().Invoke(bare);

        Assert.Equal(
            new LabResponse(20, 14, null, null, null, null, null, null, null, null, null),
            response);
    }

    [Fact]
    public void ResultToResponseCarriesTheResultsColumns()
    {
        var result = new LabTestResult
        {
            Id = 31,
            LabTestId = 21,
            OrganismId = 5,
            ResultValue = "1200",
            TestResult = "Positive/Reactive",
            Comment = "Above reference range",
            Units = "U/L",
            PrimaryLabResult = true,
        };

        var response = LabEndpoints.ResultToResponse.Compile().Invoke(result);

        Assert.Equal(
            new LabTestResultResponse(
                31, 21, 5, "1200", "Positive/Reactive", "Above reference range", "U/L", true),
            response);
    }

    [Fact]
    public void ResistToResponseCarriesTheSusceptibilityColumns()
    {
        var resist = new LabResistTest
        {
            Id = 41,
            LabId = 11,
            OrganismId = 5,
            ResistTestAgentId = 7,
            ResistTestResultId = 2,
            TestDate = new DateOnly(2026, 3, 10),
        };

        var response = LabEndpoints.ResistToResponse.Compile().Invoke(resist);

        Assert.Equal(
            new LabResistTestResponse(41, 11, 5, 7, 2, new DateOnly(2026, 3, 10)),
            response);
    }

    /// <summary>
    /// A test carries the results it is handed: the test's own columns map, and
    /// the results read by their own query hang off it in order.
    /// </summary>
    [Fact]
    public void RespondTestCarriesTheTestsColumnsAndItsResults()
    {
        var test = new LabTest
        {
            Id = 21,
            LabId = 11,
            TestTypeId = 3,
            TestStatusId = 1,
            LoincCode = "5195-3",
            ReferenceRange = "0-40",
            LabTestDate = new DateOnly(2026, 3, 9),
        };

        IReadOnlyList<LabTestResultResponse> results =
        [
            new LabTestResultResponse(31, 21, 5, "1200", "Positive/Reactive", null, "U/L", true),
        ];

        var response = LabEndpoints.RespondTest(test, results);

        Assert.Equal(
            new LabTestResponse(21, 11, 3, 1, "5195-3", "0-40", new DateOnly(2026, 3, 9), results),
            response);
    }

    /// <summary>
    /// A test with no results yet: the test maps and its results come back as
    /// the empty list it was handed.
    /// </summary>
    [Fact]
    public void RespondTestCarriesATestWithNoResults()
    {
        var test = new LabTest { Id = 22, LabId = 11 };

        var response = LabEndpoints.RespondTest(test, []);

        Assert.Empty(response.Results);
        Assert.Equal(22, response.Id);
    }

    [Fact]
    public void WhyNotALabAcceptsASpecimenWithTestsAndResults()
    {
        var request = new CreateLabRequest(
            LabFacilityId: 11,
            SpecimenSourceId: 12,
            CollectionDate: new DateOnly(2026, 3, 8),
            AccessionNo: "LAB-2026-011",
            Tests:
            [
                new CreateLabTestRequest(
                    TestTypeId: 3,
                    Results:
                    [
                        new CreateLabTestResultRequest(TestResult: "Positive/Reactive", PrimaryLabResult: true),
                        new CreateLabTestResultRequest(TestResult: "Negative/Non-reactive"),
                    ]),
            ]);

        Assert.Null(LabEndpoints.WhyNotALab(request));
    }

    /// <summary>
    /// A bare specimen — no tests, no susceptibility — is accepted: a specimen
    /// can be recorded before any test is run on it.
    /// </summary>
    [Fact]
    public void WhyNotALabAcceptsABareSpecimen()
    {
        Assert.Null(LabEndpoints.WhyNotALab(new CreateLabRequest()));
    }

    /// <summary>
    /// A test with two results marked the primary one is refused: the primary
    /// result is the one a summary shows, and there is only one summary.
    /// </summary>
    [Fact]
    public void WhyNotALabRefusesATestWithTwoPrimaryResults()
    {
        var request = new CreateLabRequest(
            Tests:
            [
                new CreateLabTestRequest(
                    Results:
                    [
                        new CreateLabTestResultRequest(TestResult: "Positive/Reactive", PrimaryLabResult: true),
                        new CreateLabTestResultRequest(TestResult: "Resistant", PrimaryLabResult: true),
                    ]),
            ]);

        Assert.Contains("results marked the primary one",
            LabEndpoints.WhyNotALab(request),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// One primary result is the usual case and is accepted: the refusal is of
    /// two, not of one.
    /// </summary>
    [Fact]
    public void WhyNotALabAcceptsATestWithOnePrimaryResult()
    {
        var request = new CreateLabRequest(
            Tests:
            [
                new CreateLabTestRequest(
                    Results: [new CreateLabTestResultRequest(PrimaryLabResult: true)]),
            ]);

        Assert.Null(LabEndpoints.WhyNotALab(request));
    }
}
