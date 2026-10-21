using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The specimen a message writes onto a case: what
/// <see cref="MessageCaseWriter.Lab"/> makes of a message in memory and the
/// codes on file. Writing it, and the transaction it is written in, are the
/// integration suite's.
/// </summary>
public class MessageCaseWriterTests
{
    private static readonly LabCodes Codes = new(
        LabFacilityId: 5,
        [
            new LabCode(CodeLists.SpecimenSource, "BD", 537),
            new LabCode(CodeLists.TestStatus, "P", 677),
            new LabCode(CodeLists.TestStatus, "F", 678),
            new LabCode(CodeLists.ResistTestAgent, "Vancomycin", 1048),
            new LabCode(CodeLists.ResistTestResult, "Susceptible", 1049),
            new LabCode(CodeLists.ResistTestResult, "Resistant", 1050),
        ]);

    private static StagedMessage Message(params StagedMessageResult[] results) => new()
    {
        AccessionNo = "ACC-1",
        CollectedOn = new DateOnly(2026, 10, 1),
        ObservedOn = new DateOnly(2026, 10, 3),
        SpecimenSourceCode = "bd",
        Results = [.. results],
    };

    [Fact]
    public void LabWritesTheSpecimenWithItsFacilityAndSource()
    {
        var lab = MessageCaseWriter.Lab(Message(), Codes)!;

        Assert.Equal(5, lab.LabFacilityId);
        Assert.Equal(537, lab.SpecimenSourceId);
        Assert.Equal(new DateOnly(2026, 10, 1), lab.CollectionDate);
        Assert.Equal("ACC-1", lab.AccessionNo);
        Assert.Empty(lab.Tests!);
        Assert.Empty(lab.ResistTests!);
    }

    /// <summary>
    /// Each result is a test of its own, under its master LOINC where it has
    /// one, with its status and the one result it reported; the first is the
    /// primary result.
    /// </summary>
    [Fact]
    public void LabWritesATestPerResult()
    {
        var lab = MessageCaseWriter.Lab(
            Message(
                new StagedMessageResult
                {
                    Sequence = 1,
                    ObservationCode = "LOCAL-1",
                    MasterLoinc = "625-4",
                    ResultStatus = "F",
                    ReferenceRange = "neg",
                    ResultValue = "Detected",
                    Interpretation = "Positive",
                    Units = "ct",
                    OrganismId = 9,
                },
                new StagedMessageResult { Sequence = 2, ObservationCode = "6463-4", ResultStatus = "P", CodedResult = "Negative" }),
            Codes)!;

        Assert.Collection(
            lab.Tests!,
            test =>
            {
                Assert.Equal("625-4", test.LoincCode);
                Assert.Equal(678, test.TestStatusId);
                Assert.Equal("neg", test.ReferenceRange);
                Assert.Equal(new DateOnly(2026, 10, 3), test.LabTestDate);
                var result = Assert.Single(test.Results!);
                Assert.Equal(new CreateLabTestResultRequest(9, "Detected", "Positive", null, "ct", true), result);
            },
            test =>
            {
                Assert.Equal("6463-4", test.LoincCode);
                Assert.Equal(677, test.TestStatusId);
                var result = Assert.Single(test.Results!);
                Assert.Equal("Negative", result.TestResult);
                Assert.False(result.PrimaryLabResult);
            });
    }

    /// <summary>
    /// A result naming a susceptibility agent is a susceptibility on the
    /// organism the other results found, not a test.
    /// </summary>
    [Fact]
    public void LabWritesASusceptibilityForAResultNamingAnAgent()
    {
        var lab = MessageCaseWriter.Lab(
            Message(
                new StagedMessageResult { Sequence = 1, ObservationCode = "11475-1", OrganismId = 9 },
                new StagedMessageResult { Sequence = 2, ObservationName = "Vancomycin [Susceptibility]", AbnormalFlag = "R" }),
            Codes)!;

        Assert.Single(lab.Tests!);
        Assert.Equal(
            new CreateLabResistTestRequest(9, 1048, 1050, new DateOnly(2026, 10, 3)),
            Assert.Single(lab.ResistTests!));
    }

    [Fact]
    public void LabIsNullForAMessageWithNothingToWrite()
    {
        Assert.Null(MessageCaseWriter.Lab(new StagedMessage(), Codes));
    }

    [Fact]
    public void FindReadsACodeIgnoringCaseAndAnUnknownOneAsNull()
    {
        Assert.Equal(678, Codes.Find(CodeLists.TestStatus, " f "));
        Assert.Null(Codes.Find(CodeLists.TestStatus, "X"));
        Assert.Null(Codes.Find(CodeLists.TestStatus, null));
    }
}
