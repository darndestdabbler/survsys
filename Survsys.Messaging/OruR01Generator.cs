using System.Globalization;
using NHapi.Base.Parser;
using NHapi.Base.Validation.Implementation;
using NHapi.Model.V251.Datatype;
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
/// one OBX for every remaining resolved variable. The Arbo guide's two-OBR
/// variant and the conditional NK1 mother segment are a later concern.
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
    private const string DefaultGuideServiceCode = "68991-9";
    private const string DefaultGuideServiceText = "Epidemiologic Information";
    private const string DefaultGuideServiceCodingSystem = "LN";

    // The PHIN variables the mapping pre-processes into named PID or OBR fields
    // rather than OBX segments, per HL7_FIELD_MAPPINGS.md §2.2.
    private const string ResultStatusVariable = "NOT118";

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
        var orderObservation = patientResult.GetORDER_OBSERVATION(0);
        WriteOrder(orderObservation.OBR);

        var observationIndex = 0;
        var raceIndex = 0;
        var ethnicIndex = 0;
        foreach (var value in input.Values)
        {
            if (PlaceInNamedField(pid, orderObservation.OBR, value, input.CountyFips, ref raceIndex, ref ethnicIndex))
            {
                continue;
            }

            var obx = orderObservation.GetOBSERVATION(observationIndex).OBX;
            WriteObservation(message, obx, value, observationIndex + 1);
            observationIndex++;
        }

        return _parser.Encode(message);
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

    /// <summary>One OBR for the default guide: its set id and the Epidemiologic Information service code.</summary>
    private static void WriteOrder(OBR obr)
    {
        obr.SetIDOBR.Value = "1";
        obr.UniversalServiceIdentifier.Identifier.Value = DefaultGuideServiceCode;
        obr.UniversalServiceIdentifier.Text.Value = DefaultGuideServiceText;
        obr.UniversalServiceIdentifier.NameOfCodingSystem.Value = DefaultGuideServiceCodingSystem;
    }

    /// <summary>
    /// Places <paramref name="value"/> into its named PID or OBR field when the
    /// mapping names one, returning whether it did. A named value is consumed
    /// here and not emitted as an OBX. The county is written as its FIPS code
    /// where <paramref name="countyFips"/> has one, and as given otherwise.
    /// </summary>
    private static bool PlaceInNamedField(
        PID pid,
        OBR obr,
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
                obr.ResultStatus.Value = value.Value;
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
