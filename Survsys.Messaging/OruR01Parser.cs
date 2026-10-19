using System.Globalization;
using NHapi.Base;
using NHapi.Base.Model;
using NHapi.Base.Parser;
using NHapi.Base.Validation.Implementation;
using NHapi.Model.V251.Datatype;
using NHapi.Model.V251.Message;
using NHapi.Model.V251.Segment;
using Survsys.Contracts;

namespace Survsys.Messaging;

/// <summary>
/// Parses a raw HL7 v2.5.1 ORU^R01 lab-result message into the
/// Survsys.Contracts intake records. This is the inbound half of what Mirth
/// did in the legacy pipeline, absorbed rather than kept: the preprocessing
/// Mirth performed before a parser ever saw the message is folded into
/// <see cref="Preprocess"/>, and nHapi parses directly to objects, so there is
/// no integration engine, no intermediate master XML and no SOAP hop.
/// </summary>
/// <remarks>
/// The reference-table ids on the intake records — the facility, specimen
/// source, test type, status and organism — are left null, exactly as those
/// records document for the fields whose lookup tables do not exist yet. The
/// codes the message carries (LOINC, the coded result) are carried through as
/// the text the message gives; matching them to vocabulary is a later
/// session's work, not this parser's.
/// </remarks>
public sealed class OruR01Parser
{
    // An empty validation context: nHapi parses the structure but enforces no
    // conformance rules, which is the leniency Mirth gave real lab messages on
    // the way in. A message that is not parseable at all still throws.
    private readonly PipeParser _parser = new() { ValidationContext = new ValidationContextImpl() };

    /// <summary>
    /// Parse one ER7 ORU^R01 message. Throws <see cref="Hl7ParseException"/>
    /// when the text is not a parseable HL7 v2 message or is of some type
    /// other than ORU^R01.
    /// </summary>
    public OruR01ParseResult Parse(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var er7 = Preprocess(raw);
        if (er7.Length == 0)
        {
            throw new Hl7ParseException("The message was empty once preprocessed.");
        }

        IMessage message;
        try
        {
            message = _parser.Parse(er7);
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

        return new OruR01ParseResult(ReadPerson(oru), ReadLab(oru));
    }

    /// <summary>
    /// The preprocessing Mirth performed on the way in, kept to what the
    /// inbound ELR message needs to parse: the escaped carriage return
    /// (<c>\015</c>) and any line ending become the single CR that nHapi
    /// requires between segments, each segment is trimmed of surrounding
    /// whitespace, and the empty segments the legacy cleanup dropped are left
    /// out. It is not a per-lab transformation pipeline.
    /// </summary>
    private static string Preprocess(string raw)
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

    private static CreatePersonRequest ReadPerson(ORU_R01 oru)
    {
        var pid = oru.GetPATIENT_RESULT(0).PATIENT.PID;
        var name = pid.GetPatientName(0);

        return new CreatePersonRequest(
            LastName: NullIfEmpty(name.FamilyName.Surname.Value),
            FirstName: NullIfEmpty(name.GivenName.Value),
            MiddleName: NullIfEmpty(name.SecondAndFurtherGivenNamesOrInitialsThereof.Value),
            BirthDate: ParseHl7Date(pid.DateTimeOfBirth.Time.Value),
            BirthGender: NullIfEmpty(pid.AdministrativeSex.Value));
    }

    private static CreateLabRequest ReadLab(ORU_R01 oru)
    {
        var patientResult = oru.GetPATIENT_RESULT(0);

        var tests = new List<CreateLabTestRequest>();
        DateOnly? collectionDate = null;
        string? accessionNo = null;

        for (var order = 0; order < patientResult.ORDER_OBSERVATIONRepetitionsUsed; order++)
        {
            var orderObservation = patientResult.GetORDER_OBSERVATION(order);
            var obr = orderObservation.OBR;

            var observedOn = ParseHl7Date(obr.ObservationDateTime.Time.Value);
            collectionDate ??= observedOn;
            accessionNo ??= NullIfEmpty(obr.FillerOrderNumber.EntityIdentifier.Value);

            var loincCode = NullIfEmpty(obr.UniversalServiceIdentifier.Identifier.Value);
            string? referenceRange = null;
            var results = new List<CreateLabTestResultRequest>();

            for (var observation = 0; observation < orderObservation.OBSERVATIONRepetitionsUsed; observation++)
            {
                var obx = orderObservation.GetOBSERVATION(observation).OBX;
                loincCode ??= NullIfEmpty(obx.ObservationIdentifier.Identifier.Value);
                referenceRange ??= NullIfEmpty(obx.ReferencesRange.Value);

                var (resultValue, codedResult) = ReadObservationValue(obx);
                results.Add(new CreateLabTestResultRequest(
                    OrganismId: null,
                    ResultValue: resultValue,
                    TestResult: codedResult,
                    Comment: null,
                    Units: ReadUnits(obx),
                    PrimaryLabResult: results.Count == 0));
            }

            tests.Add(new CreateLabTestRequest(
                TestTypeId: null,
                TestStatusId: null,
                LoincCode: loincCode,
                ReferenceRange: referenceRange,
                LabTestDate: observedOn,
                Results: results));
        }

        return new CreateLabRequest(
            CollectionDate: collectionDate,
            AccessionNo: accessionNo,
            Tests: tests);
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
