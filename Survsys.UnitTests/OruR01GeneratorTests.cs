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

    /// <summary>The PID-11.9 a notification carrying this county (DEM165) is generated with.</summary>
    internal static string CountyInPid(string? county, IReadOnlyDictionary<string, string>? countyFips)
    {
        var notification = BuildChlamydiaNotification();
        var input = notification with
        {
            Values = [.. notification.Values, new("DEM165", "County", "ST", county, null, null, null, 0)],
            CountyFips = countyFips,
        };
        var pid = Assert.Single(
            Segments(new OruR01Generator().Generate(input)), segment => segment.StartsWith("PID", StringComparison.Ordinal));

        return pid.Split('|')[11].Split('^')[8];
    }

    [Fact]
    public void WritesTheCountyAsItsFipsCode()
    {
        Assert.Equal("49035", CountyInPid("SL", new Dictionary<string, string> { ["SL"] = "49035" }));
    }

    [Fact]
    public void WritesACountyWithNoFipsCodeAsGiven()
    {
        Assert.Equal("SL", CountyInPid("SL", new Dictionary<string, string> { ["WB"] = "49057" }));
        Assert.Equal("SL", CountyInPid("SL", null));
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

    [Fact]
    public void WritesTheArboGuideAsAPersonSubjectAndANotificationObr()
    {
        var obr = Segments(new OruR01Generator().Generate(BuildLegacyExample(OruR01Generator.ArboGuideProfile)))
            .Where(segment => segment.StartsWith("OBR", StringComparison.Ordinal))
            .Select(segment => segment.Split('|'))
            .ToList();

        Assert.Equal(2, obr.Count);
        Assert.Equal("1", obr[0][1]);
        Assert.Equal("PERSUBJ^Person Subject^2.16.840.1.114222.4.5.274", obr[0][4]);
        Assert.Equal("2", obr[1][1]);
        Assert.Equal("NOTF^Individual Case Notification^2.16.840.1.114222.4.5.274", obr[1][4]);
        Assert.All(obr, fields => Assert.Equal("F", fields[25]));   // NOT118 on each OBR
    }

    [Fact]
    public void PutsTheArboGuidesDemographicObservationsUnderItsPersonSubjectObr()
    {
        var segments = Segments(new OruR01Generator().Generate(BuildLegacyExample(OruR01Generator.ArboGuideProfile)));
        var notificationObr = Array.FindIndex(segments, segment => segment.StartsWith("OBR|2|", StringComparison.Ordinal));

        var underPersonSubject = segments[..notificationObr].Where(IsObx).Select(segment => segment.Split('|')).ToList();
        var underNotification = segments[notificationObr..].Where(IsObx).Select(segment => segment.Split('|')).ToList();

        var demographic = Assert.Single(underPersonSubject);
        Assert.Equal("1", demographic[1]);
        Assert.StartsWith("DEM", demographic[3], StringComparison.Ordinal);
        Assert.Equal(["1", "2"], underNotification.Select(fields => fields[1]));    // set ids restart per OBR
        Assert.Equal(["INV163", "NOT116"], underNotification.Select(fields => fields[3].Split('^')[0]));
    }

    [Fact]
    public void WritesTheDefaultGuideWithOneObrHoweverManyDemographicsItCarries()
    {
        var segments = Segments(new OruR01Generator().Generate(BuildLegacyExample("Generic MMG v2.0")));

        Assert.Single(segments, segment => segment.StartsWith("OBR", StringComparison.Ordinal));
        Assert.Equal(3, segments.Count(IsObx));
    }

    [Fact]
    public void WritesTheMotherAsAnNk1SegmentAndNotAsObservations()
    {
        var example = BuildLegacyExample("Generic MMG v2.0");
        var input = example with
        {
            Values =
            [
                .. example.Values,
                new("MTH153", "Mother's Birth Date", "TS", "19700412", null, null, null, 0),
                new("MTH157", "Mother's Race", "CWE", "White", "2106-3", "2.16.840.1.113883.6.238", null, 0),
                new("MTH159", "Mother's Ethnicity", "CWE", "Not Hispanic or Latino", "2186-5", "2.16.840.1.113883.6.238", null, 0),
                new("MTH165", "Mother's Marital Status", "CWE", "Married", "M", "2.16.840.1.113883.12.2", null, 0),
                new("MTH166", "Mother's State", "CWE", "Connecticut", "09", "2.16.840.1.113883.6.92", null, 0),
                new("MTH167", "Mother's Country", "CWE", "United States", "USA", "1.0.3166.1", null, 0),
                new("MTH168", "Mother's County", "CWE", "Hartford", "09003", "2.16.840.1.113883.6.93", null, 0),
                new("MTH169", "Mother's Zip Code", "ST", "06106", null, null, null, 0),
            ],
        };

        var segments = Segments(new OruR01Generator().Generate(input));
        var nk1 = Assert.Single(segments, segment => segment.StartsWith("NK1", StringComparison.Ordinal)).Split('|');

        Assert.Equal("1", nk1[1]);
        Assert.Equal("MTH^Mother^HL70063", nk1[3]);
        Assert.Equal("^^^09^06106^USA^^^09003", nk1[4]);
        Assert.Equal("M^Married^2.16.840.1.113883.12.2", nk1[14]);
        Assert.Equal("19700412", nk1[16]);
        Assert.Equal("2186-5^Not Hispanic or Latino^2.16.840.1.113883.6.238", nk1[28]);
        Assert.Equal("2106-3^White^2.16.840.1.113883.6.238", nk1[35]);
        Assert.DoesNotContain(segments, segment => IsObx(segment) && segment.Contains("|MTH", StringComparison.Ordinal));
    }

    [Fact]
    public void WritesNoNk1WhenNoMotherVariableHasAValue()
    {
        var example = BuildLegacyExample("Generic MMG v2.0");
        var input = example with
        {
            Values = [.. example.Values, new("MTH169", "Mother's Zip Code", "ST", " ", null, null, null, 0)],
        };

        var segments = Segments(new OruR01Generator().Generate(input));

        Assert.DoesNotContain(segments, segment => segment.StartsWith("NK1", StringComparison.Ordinal));
        Assert.DoesNotContain(segments, segment => IsObx(segment) && segment.Contains("|MTH", StringComparison.Ordinal));
    }

    private static bool IsObx(string segment) => segment.StartsWith("OBX", StringComparison.Ordinal);

    /// <summary>
    /// The record in NMI_XML_GENERATION.md's <i>Complete Output Structure</i>:
    /// its header and its INV163 and NOT116 variables, with a patient
    /// identifier and a race so the person-subject OBR has an observation.
    /// </summary>
    private static OruR01GenerationInput BuildLegacyExample(string guideProfile)
    {
        var header = new NotificationHeader(
            SendingApp: new NotificationIdentifier("ELR Application", "2.16.840.1.114222.X", "ISO"),
            SendingFacility: new NotificationIdentifier("UT DOH", "2.16.840.1.114222.X", "ISO"),
            ReceivingApp: new NotificationIdentifier("PHINCDS", "2.16.840.1.114222.X", "ISO"),
            ReceivingFacility: new NotificationIdentifier("PHIN", "2.16.840.1.114222.X", "ISO"),
            SendingTime: new DateTimeOffset(2026, 3, 13, 14, 30, 22, TimeSpan.Zero),
            ProcessingId: "P",
            Version: "2.5.1",
            ProfileId: guideProfile == OruR01Generator.ArboGuideProfile ? OruR01Generator.ArboProfileId : "NOTF_ORU_v3.0",
            ControlId: "SURVSYS0000000042");

        return new OruR01GenerationInput(header, guideProfile,
        [
            new("DEM197", "Patient Identifier", "ST", "202600042", null, null, null, 0),
            new("DEM2", "Age Reported at Time of Case Investigation", "NM", "35", null, null, null, 0),
            new("NOT118", "Result Status", "ST", "F", null, null, null, 0),
            new("INV163", "Age at time of case investigation", "CE", "35", "Y", "2.16.840.1.113883.6.1", null, 1),
            new("NOT116", "Local Record ID", "ST", "202600042", null, null, null, 0),
        ]);
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
