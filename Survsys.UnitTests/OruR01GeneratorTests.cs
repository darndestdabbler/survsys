using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The ORU^R01 generator proved against a legacy generated message: a
/// default-guide Chlamydia notification whose ER7 the generator must reproduce
/// from the same resolved PHIN variable values, together with the structural
/// invariants of the message it builds. The fixture is authored from the
/// PHIN-variable to HL7-field mapping in
/// ../nedss/docs/business-logic/HL7_FIELD_MAPPINGS.md §2.2, with a fixed control
/// id and sending time so the generated message is deterministic.
/// </summary>
public class OruR01GeneratorTests
{
    private static readonly string FixtureRoot =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Hl7");

    private static string ReadFixture(string name) =>
        File.ReadAllText(Path.Combine(FixtureRoot, name));

    // The ER7 segment separator is a carriage return and a fixture file is
    // stored one segment per line, so both sides are read as a sequence of
    // segments rather than as a single run of bytes whose line endings a commit
    // might have rewritten.
    private static string[] Segments(string er7) =>
        er7.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static string[] Generate() =>
        Segments(new OruR01Generator().Generate(BuildChlamydiaNotification()));

    [Fact]
    public void GeneratesTheDefaultGuideNotificationTheLegacySystemGenerates()
    {
        var generated = Generate();
        var expected = Segments(ReadFixture("oru-r01-notification-chlamydia.hl7"));

        Assert.Equal(expected, generated);
    }

    [Fact]
    public void BuildsAnOruR01HeaderCarryingTheHeadersControlIdAndTime()
    {
        var msh = Assert.Single(Generate(), segment => segment.StartsWith("MSH", StringComparison.Ordinal));
        var fields = msh.Split('|');

        // MSH-1 is the field separator, so MSH-9 is index 8 and MSH-10 index 9.
        Assert.Equal("ORU^R01^ORU_R01", fields[8]);
        Assert.Equal("SURVSYS0000000001", fields[9]);
        Assert.Equal("20260115103000", fields[6]);
    }

    [Fact]
    public void PlacesTheDemographicVariablesInTheirNamedPidFields()
    {
        var pid = Assert.Single(Generate(), segment => segment.StartsWith("PID", StringComparison.Ordinal));
        var fields = pid.Split('|');

        Assert.Equal("CASE-2026-000042", fields[3]);                       // DEM197 -> PID-3.1
        Assert.Equal("19900101", fields[7]);                               // DEM115 -> PID-7
        Assert.Equal("F", fields[8]);                                      // DEM113 -> PID-8
        Assert.Equal("N^Not Hispanic or Latino^2.16.840.1.114222.4.5.1", fields[22]); // DEM155 -> PID-22
    }

    [Fact]
    public void WritesExactlyOneObrCarryingTheEpidemiologicInformationService()
    {
        var obr = Assert.Single(Generate(), segment => segment.StartsWith("OBR", StringComparison.Ordinal));
        var fields = obr.Split('|');

        Assert.Equal("68991-9^Epidemiologic Information^LN", fields[4]);   // OBR-4
        Assert.Equal("F", fields[25]);                                     // NOT118 -> OBR-25
    }

    [Fact]
    public void WritesOneObxPerRemainingVariableWithAnIncrementingSetId()
    {
        var obx = Generate()
            .Where(segment => segment.StartsWith("OBX", StringComparison.Ordinal))
            .ToList();

        // The four named variables (DEM197, DEM115, DEM113, DEM155) and the
        // result status (NOT118) are placed in PID and OBR; the remaining three
        // become one OBX each.
        Assert.Equal(3, obx.Count);
        Assert.Equal("1", obx[0].Split('|')[1]);
        Assert.Equal("2", obx[1].Split('|')[1]);
        Assert.Equal("3", obx[2].Split('|')[1]);
        Assert.Equal("INV163^Case Status", obx[0].Split('|')[3]);
        Assert.Equal("LAB278^Resulted Organism", obx[2].Split('|')[3]);
        Assert.Equal("1", obx[2].Split('|')[4]);                           // the repeating-group sub-id
    }

    internal static OruR01GenerationInput BuildChlamydiaNotification()
    {
        var header = new NotificationHeader(
            SendingApp: new NotificationIdentifier("Survsys", "2.16.840.1.114222.4.3.3.2.1", "ISO"),
            SendingFacility: new NotificationIdentifier("CTDPH", "2.16.840.1.114222.4.1.3650", "ISO"),
            ReceivingApp: new NotificationIdentifier("CDC", "2.16.840.1.114222.4.3.2.10", "ISO"),
            ReceivingFacility: new NotificationIdentifier("CDC", "2.16.840.1.114222.4.1.3650", "ISO"),
            SendingTime: new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero),
            ProcessingId: "P",
            Version: "2.5.1",
            ProfileId: "NOTF_ORU_v3.0",
            ControlId: "SURVSYS0000000001");

        var values = new List<NotificationValue>
        {
            new("DEM197", "Patient Identifier", "ST", "CASE-2026-000042", null, null, null, 0),
            new("DEM115", "Date of Birth", "TS", "19900101", null, null, null, 0),
            new("DEM113", "Administrative Sex", "ST", "F", null, null, null, 0),
            new("DEM155", "Ethnic Group", "CWE", "Not Hispanic or Latino", "N", "2.16.840.1.114222.4.5.1", null, 0),
            new("NOT118", "Result Status", "ST", "F", null, null, null, 0),
            new("INV163", "Case Status", "CWE", "Confirmed", "410605003", "2.16.840.1.113883.6.96", null, 0),
            new("INV178", "MMWR Week", "NM", "3", null, null, null, 0),
            new("LAB278", "Resulted Organism", "CWE", "Chlamydia trachomatis", "63938009", "2.16.840.1.113883.6.96", null, 1),
        };

        return new OruR01GenerationInput(header, "NOTF_ORU_v3.0", values);
    }
}
