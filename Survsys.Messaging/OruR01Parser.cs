using System.Globalization;
using NHapi.Base;
using NHapi.Base.Model;
using NHapi.Base.Parser;
using NHapi.Base.Validation.Implementation;
using NHapi.Model.V251.Datatype;
using NHapi.Model.V251.Group;
using NHapi.Model.V251.Message;
using NHapi.Model.V251.Segment;
using Survsys.Contracts;

namespace Survsys.Messaging;

/// <summary>
/// Parses a raw HL7 v2.5.1 ORU^R01 lab-result message into the
/// <see cref="ParsedMessage"/> intake keeps — the HL7 v2 implementation of
/// <see cref="IMessageParser"/>. This is the inbound half of what Mirth
/// did in the legacy pipeline, absorbed rather than kept: the preprocessing
/// Mirth performed before a parser ever saw the message is folded into
/// <see cref="Preprocess"/>, and nHapi parses directly to objects, so there is
/// no integration engine, no intermediate master XML and no SOAP hop.
/// </summary>
/// <remarks>
/// The codes the message carries (LOINC, the specimen type, the coded result)
/// are carried through as the text the message gives; matching them to
/// vocabulary is a later session's work, not this parser's. Each patient-result
/// group becomes one <see cref="ParsedPatient"/>, and each OBR under it one
/// <see cref="ParsedOrder"/>, with the first SPM under it as its specimen.
/// </remarks>
public sealed class OruR01Parser : IMessageParser
{
    /// <summary>The format this parser reads, as it is recorded on a staged message.</summary>
    public const string Hl7V2Format = "HL7v2";

    /// <inheritdoc />
    public string Format => Hl7V2Format;

    /// <summary>
    /// Parse one ER7 ORU^R01 message. Throws <see cref="Hl7ParseException"/>
    /// when the text is not a parseable HL7 v2 message or is of some type
    /// other than ORU^R01.
    /// </summary>
    public ParsedMessage Parse(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var er7 = Preprocess(raw);
        if (er7.Length == 0)
        {
            throw new Hl7ParseException("The message was empty once preprocessed.");
        }

        // An empty validation context: nHapi parses the structure but enforces
        // no conformance rules, which is the leniency Mirth gave real lab
        // messages on the way in. A message that is not parseable at all still
        // throws. A parser per call, so one OruR01Parser is safe to share
        // between the intake's concurrent receivers.
        var parser = new PipeParser { ValidationContext = new ValidationContextImpl() };

        IMessage message;
        try
        {
            message = parser.Parse(er7);
        }
        catch (HL7Exception ex)
        {
            throw new Hl7ParseException("The message could not be parsed as HL7 v2.", ex);
        }

        if (message is not ORU_R01 oru)
        {
            throw new Hl7ParseException(
                $"Expected an ORU^R01 message but the message parsed as {message.GetType().Name}.");
        }

        var msh = oru.MSH;
        var messageCode = NullIfEmpty(msh.MessageType.MessageCode.Value);
        var triggerEvent = NullIfEmpty(msh.MessageType.TriggerEvent.Value);

        var patients = Enumerable
            .Range(0, oru.PATIENT_RESULTRepetitionsUsed)
            .Select(index => ReadPatient(oru.GetPATIENT_RESULT(index)))
            .ToList();

        return new ParsedMessage(
            Format: Format,
            MessageType: triggerEvent is null ? messageCode : $"{messageCode}^{triggerEvent}",
            SendingFacility: NullIfEmpty(msh.SendingFacility.NamespaceID.Value),
            Patients: patients);
    }

    /// <summary>
    /// The preprocessing Mirth performed on the way in, kept to what the
    /// inbound ELR message needs to parse: the escaped carriage return
    /// (<c>\015</c>) and any line ending become the single CR that nHapi
    /// requires between segments, each segment is trimmed of surrounding
    /// whitespace, and the empty segments the legacy cleanup dropped are left
    /// out. It is not a per-lab transformation pipeline. Shared with
    /// <see cref="Er7Message"/>, so a conformance check reads the segments
    /// this parser reads.
    /// </summary>
    internal static string Preprocess(string raw)
    {
        var unescaped = raw
            .Replace("\\015", "\r", StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        var segments = unescaped
            .Split('\n')
            .Select(segment => segment.Trim())
            .Where(segment => segment.Length > 0);

        return string.Join('\r', segments);
    }

    private static ParsedPatient ReadPatient(ORU_R01_PATIENT_RESULT patientResult)
    {
        var pid = patientResult.PATIENT.PID;
        var address = pid.GetPatientAddress(0);

        return new ParsedPatient(
            Person: ReadPerson(pid),
            Street: NullIfEmpty(address.StreetAddress.StreetOrMailingAddress.Value),
            City: NullIfEmpty(address.City.Value),
            State: NullIfEmpty(address.StateOrProvince.Value),
            Zip: NullIfEmpty(address.ZipOrPostalCode.Value),
            Orders: ReadOrders(patientResult));
    }

    private static CreatePersonRequest ReadPerson(PID pid)
    {
        var name = pid.GetPatientName(0);

        return new CreatePersonRequest(
            LastName: NullIfEmpty(name.FamilyName.Surname.Value),
            FirstName: NullIfEmpty(name.GivenName.Value),
            MiddleName: NullIfEmpty(name.SecondAndFurtherGivenNamesOrInitialsThereof.Value),
            BirthDate: ParseHl7Date(pid.DateTimeOfBirth.Time.Value),
            BirthGender: NullIfEmpty(pid.AdministrativeSex.Value));
    }

    private static IReadOnlyList<ParsedOrder> ReadOrders(ORU_R01_PATIENT_RESULT patientResult)
    {
        var orders = new List<ParsedOrder>();

        for (var order = 0; order < patientResult.ORDER_OBSERVATIONRepetitionsUsed; order++)
        {
            var orderObservation = patientResult.GetORDER_OBSERVATION(order);
            var obr = orderObservation.OBR;
            var observedOn = ParseHl7Date(obr.ObservationDateTime.Time.Value);

            var results = new List<ParsedResult>();
            for (var observation = 0; observation < orderObservation.OBSERVATIONRepetitionsUsed; observation++)
            {
                var obx = orderObservation.GetOBSERVATION(observation).OBX;
                var (resultValue, codedResult) = ReadObservationValue(obx);
                var abnormalFlags = obx.GetAbnormalFlags();

                results.Add(new ParsedResult(
                    ObservationCode: NullIfEmpty(obx.ObservationIdentifier.Identifier.Value),
                    ObservationName: NullIfEmpty(obx.ObservationIdentifier.Text.Value),
                    ResultValue: resultValue,
                    CodedResult: codedResult,
                    Units: ReadUnits(obx),
                    ReferenceRange: NullIfEmpty(obx.ReferencesRange.Value),
                    AbnormalFlag: abnormalFlags.Length == 0 ? null : NullIfEmpty(abnormalFlags[0].Value),
                    ResultStatus: NullIfEmpty(obx.ObservationResultStatus.Value)));
            }

            SPM? spm = orderObservation.SPECIMENRepetitionsUsed > 0
                ? orderObservation.GetSPECIMEN(0).SPM
                : null;

            orders.Add(new ParsedOrder(
                AccessionNo: NullIfEmpty(obr.FillerOrderNumber.EntityIdentifier.Value),
                TestCode: NullIfEmpty(obr.UniversalServiceIdentifier.Identifier.Value)
                    ?? results.Select(result => result.ObservationCode).FirstOrDefault(code => code is not null),
                TestName: NullIfEmpty(obr.UniversalServiceIdentifier.Text.Value),
                ObservedOn: observedOn,
                SpecimenCode: NullIfEmpty(spm?.SpecimenType.Identifier.Value),
                SpecimenText: NullIfEmpty(spm?.SpecimenType.Text.Value),
                CollectedOn: ParseHl7Date(spm?.SpecimenCollectionDateTime.RangeStartDateTime.Time.Value) ?? observedOn,
                Results: results));
        }

        return orders;
    }

    /// <summary>
    /// OBX-5. A coded value yields both the code it carries, as the result
    /// value, and the text beside it, as the coded result; a plain value
    /// yields itself, with no coded result.
    /// </summary>
    private static (string? ResultValue, string? CodedResult) ReadObservationValue(OBX obx)
    {
        var values = obx.GetObservationValue();
        if (values.Length == 0)
        {
            return (null, null);
        }

        return values[0].Data switch
        {
            CWE coded => (NullIfEmpty(coded.Identifier.Value), NullIfEmpty(coded.Text.Value)),
            IPrimitive primitive => (NullIfEmpty(primitive.Value), null),
            { } other => (NullIfEmpty(other.ToString()), null),
            _ => (null, null),
        };
    }

    private static string? ReadUnits(OBX obx) =>
        NullIfEmpty(obx.Units.Identifier.Value) ?? NullIfEmpty(obx.Units.Text.Value);

    /// <summary>
    /// An HL7 timestamp to a date: the leading eight digits read as
    /// <c>yyyyMMdd</c>, ignoring any time or offset beyond them.
    /// </summary>
    private static DateOnly? ParseHl7Date(string? timestamp)
    {
        if (string.IsNullOrWhiteSpace(timestamp))
        {
            return null;
        }

        var digits = new string(timestamp.TakeWhile(char.IsDigit).ToArray());
        return digits.Length >= 8
            && DateOnly.TryParseExact(
                digits[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
