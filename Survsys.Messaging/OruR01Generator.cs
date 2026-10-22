using System.Globalization;
using NHapi.Base.Parser;
using NHapi.Base.Validation.Implementation;
using NHapi.Model.V251.Datatype;
using NHapi.Model.V251.Group;
using NHapi.Model.V251.Message;
using NHapi.Model.V251.Segment;

namespace Survsys.Messaging;

/// <summary>
/// Builds an HL7 v2.5.1 ORU^R01 notification from an
/// <see cref="OruR01GenerationInput"/> with nHapi and encodes it to ER7. This
/// is the outbound half of what Mirth did in the legacy pipeline, absorbed
/// rather than kept: the JavaScript code template that turned NMI's
/// intermediate XML into HL7 is folded into this generator, so there is no
/// integration engine and no intermediate XML between the resolved variables
/// and the message.
/// </summary>
/// <remarks>
/// The segment construction order follows the legacy NMI generation described
/// in <c>../nedss/docs/business-logic/NMI_XML_GENERATION.md</c> and the
/// PHIN-variable to HL7-field mapping in
/// <c>../nedss/docs/business-logic/HL7_FIELD_MAPPINGS.md</c> §2.2, for the
/// default guide: MSH from the header, PID from the demographic and death-date
/// variables, one OBR carrying the Epidemiologic Information service code, and
/// one OBX for every remaining resolved variable. The arbovirus guide
/// (<see cref="ArboGuideProfile"/>) writes two OBRs instead — PERSUBJ, carrying
/// the patient's DEM observations, and NOTF, carrying the rest — each with its
/// own OBX set ids. Where any mother (MTH) variable has a value, an NK1 mother
/// segment carries them.
///
/// The message is deterministic: the control id and the sending time are the
/// header's rather than a freshly generated id and the clock, so the same input
/// encodes to the same ER7, which is what lets a test assert a generated
/// message against a fixture. A variable the mapping names is placed into its
/// segment field and not emitted as an OBX; everything else becomes one OBX in
/// the order the values are given.
/// </remarks>
public sealed class OruR01Generator
{
    /// <summary>The guide profile whose message carries two OBRs, per HL7_FIELD_MAPPINGS.md §2.4.</summary>
    public const string ArboGuideProfile = "Arbo_Case_Map_v1.0";

    /// <summary>The message profile (MSH-21) the arbovirus guide is sent under.</summary>
    public const string ArboProfileId = "NND_ORU_v2.0";

    private const string ArboCodingSystem = "2.16.840.1.114222.4.5.274";

    private static readonly (string Code, string Text, string CodingSystem) DefaultGuideService =
        ("68991-9", "Epidemiologic Information", "LN");

    private static readonly (string Code, string Text, string CodingSystem) PersonSubjectService =
        ("PERSUBJ", "Person Subject", ArboCodingSystem);

    private static readonly (string Code, string Text, string CodingSystem) NotificationService =
        ("NOTF", "Individual Case Notification", ArboCodingSystem);

    // The PHIN variables the mapping pre-processes into named PID or OBR fields
    // rather than OBX segments, per HL7_FIELD_MAPPINGS.md §2.2.
    private const string ResultStatusVariable = "NOT118";
    private const string MotherPrefix = "MTH";
    private const string DemographicPrefix = "DEM";

    // An empty validation context, as OruR01Parser reads with: the structure is
    // encoded but no conformance rule is enforced, so a notification that omits
    // a field HL7 would require still encodes.
    private readonly PipeParser _parser = new() { ValidationContext = new ValidationContextImpl() };

    /// <summary>
    /// Generates the ER7 ORU^R01 message for <paramref name="input"/>.
    /// </summary>
    public string Generate(OruR01GenerationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var message = new ORU_R01();
        WriteHeader(message.MSH, input.Header);

        var patientResult = message.GetPATIENT_RESULT(0);
        var pid = patientResult.PATIENT.PID;
        var arbo = input.GuideProfile == ArboGuideProfile;
        var orders = arbo
            ? new[] { patientResult.GetORDER_OBSERVATION(0), patientResult.GetORDER_OBSERVATION(1) }
            : new[] { patientResult.GetORDER_OBSERVATION(0) };
        if (arbo)
        {
            WriteOrder(orders[0].OBR, 1, PersonSubjectService);
            WriteOrder(orders[1].OBR, 2, NotificationService);
        }
        else
        {
            WriteOrder(orders[0].OBR, 1, DefaultGuideService);
        }

        if (input.Values.Any(value => IsMother(value) && HasValue(value)))
        {
            WriteMother(patientResult.PATIENT.GetNK1(0), input.Values.Where(IsMother));
        }

        var observationCounts = new int[orders.Length];
        var raceIndex = 0;
        var ethnicIndex = 0;
        foreach (var value in input.Values)
        {
            if (IsMother(value)
                || PlaceInNamedField(pid, orders, value, input.CountyFips, ref raceIndex, ref ethnicIndex))
            {
                continue;
            }

            // The arbovirus guide's patient observations go under its person-subject OBR.
            var order = arbo && !value.PhinVar.StartsWith(DemographicPrefix, StringComparison.Ordinal) ? 1 : 0;
            var obx = orders[order].GetOBSERVATION(observationCounts[order]).OBX;
            WriteObservation(message, obx, value, observationCounts[order] + 1);
            observationCounts[order]++;
        }

        return _parser.Encode(message);
    }

    private static bool IsMother(NotificationValue value) =>
        value.PhinVar.StartsWith(MotherPrefix, StringComparison.Ordinal);

    private static bool HasValue(NotificationValue value) =>
        !string.IsNullOrWhiteSpace(value.Value) || !string.IsNullOrWhiteSpace(value.Code);

    /// <summary>
    /// The NK1 mother segment, per HL7_FIELD_MAPPINGS.md §2.2: set id 1, the
    /// mother relationship, and each MTH variable in its named field. A mother
    /// variable the mapping does not name is not written.
    /// </summary>
    private static void WriteMother(NK1 nk1, IEnumerable<NotificationValue> values)
    {
        nk1.SetIDNK1.Value = "1";
        nk1.Relationship.Identifier.Value = "MTH";
        nk1.Relationship.Text.Value = "Mother";
        nk1.Relationship.NameOfCodingSystem.Value = "HL70063";

        var address = nk1.GetAddress(0);
        var raceIndex = 0;
        foreach (var value in values)
        {
            switch (value.PhinVar)
            {
                case "MTH153":
                    nk1.DateTimeOfBirth.Time.Value = value.Value;
                    break;
                case "MTH157":
                    WriteCoded(nk1.GetRace(raceIndex++), value);
                    break;
                case "MTH159":
                    WriteCoded(nk1.GetEthnicGroup(0), value);
                    break;
                case "MTH165":
                    WriteCoded(nk1.MaritalStatus, value);
                    break;
                case "MTH166":
                    address.StateOrProvince.Value = value.Code ?? value.Value;
                    break;
                case "MTH167":
                    address.Country.Value = value.Code ?? value.Value;
                    break;
                case "MTH168":
                    address.CountyParishCode.Value = value.Code ?? value.Value;
                    break;
                case "MTH169":
                    address.ZipOrPostalCode.Value = value.Value;
                    break;
            }
        }
    }

    /// <summary>
    /// MSH from the header: the sending and receiving application and facility,
    /// the sending time, the fixed ORU^R01 message type, the control id,
    /// processing id, version and profile id.
    /// </summary>
    private static void WriteHeader(MSH msh, NotificationHeader header)
    {
        msh.FieldSeparator.Value = "|";
        msh.EncodingCharacters.Value = @"^~\&";

        WriteIdentifier(msh.SendingApplication, header.SendingApp);
        WriteIdentifier(msh.SendingFacility, header.SendingFacility);
        WriteIdentifier(msh.ReceivingApplication, header.ReceivingApp);
        WriteIdentifier(msh.ReceivingFacility, header.ReceivingFacility);

        msh.DateTimeOfMessage.Time.Value = FormatTimestamp(header.SendingTime);

        msh.MessageType.MessageCode.Value = "ORU";
        msh.MessageType.TriggerEvent.Value = "R01";
        msh.MessageType.MessageStructure.Value = "ORU_R01";

        msh.MessageControlID.Value = header.ControlId;
        msh.ProcessingID.ProcessingID.Value = header.ProcessingId;
        msh.VersionID.VersionID.Value = header.Version;
        msh.GetMessageProfileIdentifier(0).EntityIdentifier.Value = header.ProfileId;
    }

    /// <summary>One OBR: its set id and the service it carries.</summary>
    private static void WriteOrder(OBR obr, int setId, (string Code, string Text, string CodingSystem) service)
    {
        obr.SetIDOBR.Value = setId.ToString(CultureInfo.InvariantCulture);
        obr.UniversalServiceIdentifier.Identifier.Value = service.Code;
        obr.UniversalServiceIdentifier.Text.Value = service.Text;
        obr.UniversalServiceIdentifier.NameOfCodingSystem.Value = service.CodingSystem;
    }

    /// <summary>
    /// Places <paramref name="value"/> into its named PID or OBR field when the
    /// mapping names one, returning whether it did. A named value is consumed
    /// here and not emitted as an OBX; an OBR field is written on every OBR.
    /// The county is written as its FIPS code where <paramref name="countyFips"/>
    /// has one, and as given otherwise.
    /// </summary>
    private static bool PlaceInNamedField(
        PID pid,
        ORU_R01_ORDER_OBSERVATION[] orders,
        NotificationValue value,
        IReadOnlyDictionary<string, string>? countyFips,
        ref int raceIndex,
        ref int ethnicIndex)
    {
        switch (value.PhinVar)
        {
            case "DEM197":
                pid.GetPatientIdentifierList(0).IDNumber.Value = value.Value;
                return true;
            case "DEM115":
                pid.DateTimeOfBirth.Time.Value = value.Value;
                return true;
            case "DEM113":
                pid.AdministrativeSex.Value = value.Value;
                return true;
            case "DEM152":
                WriteCoded(pid.GetRace(raceIndex++), value);
                return true;
            case "DEM161":
                pid.GetPatientAddress(0).City.Value = value.Value;
                return true;
            case "DEM162":
                pid.GetPatientAddress(0).StateOrProvince.Value = value.Value;
                return true;
            case "DEM163":
                pid.GetPatientAddress(0).ZipOrPostalCode.Value = value.Value;
                return true;
            case "DEM165":
                pid.GetPatientAddress(0).CountyParishCode.Value =
                    value.Value is { } county && countyFips?.GetValueOrDefault(county) is { } fips ? fips : value.Value;
                return true;
            case "DEM155":
                WriteCoded(pid.GetEthnicGroup(ethnicIndex++), value);
                return true;
            case "INV146":
                pid.PatientDeathDateAndTime.Time.Value = value.Value;
                return true;
            case ResultStatusVariable:
                foreach (var order in orders)
                {
                    order.OBR.ResultStatus.Value = value.Value;
                }

                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// One OBX for a remaining variable: its set id, value type, observation
    /// identifier, the repeating-group sub-id when it repeats, and its value.
    /// </summary>
    private static void WriteObservation(ORU_R01 message, OBX obx, NotificationValue value, int setId)
    {
        obx.SetIDOBX.Value = setId.ToString(CultureInfo.InvariantCulture);
        obx.ValueType.Value = value.ValueType;
        obx.ObservationIdentifier.Identifier.Value = value.PhinVar;
        obx.ObservationIdentifier.Text.Value = value.Name;

        if (value.SubId > 0)
        {
            obx.ObservationSubID.Value = value.SubId.ToString(CultureInfo.InvariantCulture);
        }

        var observationValue = obx.GetObservationValue(0);
        if (!string.IsNullOrEmpty(value.Code))
        {
            var coded = new CWE(message);
            coded.Identifier.Value = value.Code;
            coded.Text.Value = value.Value;
            coded.NameOfCodingSystem.Value = value.CodeSystemOid;
            if (!string.IsNullOrEmpty(value.OtherSpecified))
            {
                coded.OriginalText.Value = value.OtherSpecified;
            }

            observationValue.Data = coded;
        }
        else
        {
            var text = new ST(message) { Value = value.Value };
            observationValue.Data = text;
        }
    }

    /// <summary>A coded value into a CE (the PID race and ethnic-group datatype): its code, display text and code-system OID.</summary>
    private static void WriteCoded(CE coded, NotificationValue value)
    {
        coded.Identifier.Value = value.Code ?? value.Value;
        if (!string.IsNullOrEmpty(value.Code))
        {
            coded.Text.Value = value.Value;
        }

        coded.NameOfCodingSystem.Value = value.CodeSystemOid;
    }

    /// <summary>A three-part identifier into an HD: its namespace, universal id and type.</summary>
    private static void WriteIdentifier(HD hd, NotificationIdentifier identifier)
    {
        hd.NamespaceID.Value = identifier.Namespace;
        hd.UniversalID.Value = identifier.Name;
        hd.UniversalIDType.Value = identifier.Type;
    }

    /// <summary>A moment as an HL7 timestamp, to the second, so the message is deterministic.</summary>
    private static string FormatTimestamp(DateTimeOffset moment) =>
        moment.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
}
