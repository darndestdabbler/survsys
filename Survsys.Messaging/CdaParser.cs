using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Survsys.Contracts;

namespace Survsys.Messaging;

/// <summary>
/// Parses an eCR document — an eICR R1.1, R3.1 or R3.1.1, or an RR 1.1, each a
/// CDA R2 ClinicalDocument — into a <see cref="CdaDocument"/>: the CDA
/// implementation of <see cref="IMessageParser"/>, beside
/// <see cref="OruR01Parser"/>.
/// </summary>
/// <remarks>
/// Every entry is found by its templateId root rather than by its place in
/// the document, so a section that moves between versions, or an entry nested
/// where the guide allows it, is still read. The codes are carried through as
/// the document gives them; mapping them to conditions is a later session's
/// work, not this parser's. No eCR library for .NET exists, so this is LINQ to
/// XML, with DTDs refused so a document cannot reach outside itself.
/// </remarks>
public sealed class CdaParser : IMessageParser
{
    /// <summary>The format this parser reads, as it is recorded on a staged message.</summary>
    public const string CdaFormat = "CDA";

    /// <summary>The kind of an electronic initial case report.</summary>
    public const string EicrKind = "eICR";

    /// <summary>The kind of a reportability response.</summary>
    public const string RrKind = "RR";

    private const string EicrTemplate = "2.16.840.1.113883.10.20.15.2";
    private const string RrTemplate = "2.16.840.1.113883.10.20.15.2.1.2";
    private const string ProblemObservation = "2.16.840.1.113883.10.20.22.4.4";
    private const string TriggerProblemObservation = "2.16.840.1.113883.10.20.15.2.3.3";
    private const string ResultOrganizer = "2.16.840.1.113883.10.20.22.4.1";
    private const string ResultObservation = "2.16.840.1.113883.10.20.22.4.2";
    private const string MedicationActivity = "2.16.840.1.113883.10.20.22.4.16";
    private const string PregnancyObservation = "2.16.840.1.113883.10.20.15.3.8";
    private const string TravelHistory = "2.16.840.1.113883.10.20.15.2.3.1";
    private const string SocialHistoryObservation = "2.16.840.1.113883.10.20.22.4.38";
    private const string PastOrPresentOccupation = "2.16.840.1.113883.10.20.22.4.217";
    private const string UsualOccupation = "2.16.840.1.113883.10.20.22.4.221";
    private const string EicrReference = "2.16.840.1.113883.10.20.15.2.3.10";
    private const string ReportableCondition = "2.16.840.1.113883.10.20.15.2.3.12";
    private const string ReportabilityInformation = "2.16.840.1.113883.10.20.15.2.3.13";
    private const string ResponsibleAgency = "2.16.840.1.113883.10.20.15.2.4.2";
    private const string DeterminationOfReportability = "2.16.840.1.113883.10.20.15.2.3.19";
    private const string ReportingTimeframe = "2.16.840.1.113883.10.20.15.2.3.14";

    /// <summary>The eICR versions read, by the extension of the document's templateId.</summary>
    private static readonly Dictionary<string, string> EicrVersions = new()
    {
        ["2016-12-01"] = "R1.1",
        ["2021-01-01"] = "R3.1",
        ["2022-05-01"] = "R3.1.1",
    };

    /// <summary>The RR versions read, by the extension of the document's templateId.</summary>
    private static readonly Dictionary<string, string> RrVersions = new()
    {
        ["2017-04-01"] = "1.1",
    };

    /// <summary>
    /// Social history codes that say an observation is the patient's
    /// employment: SNOMED employment detail, and LOINC current employment and
    /// history of occupation.
    /// </summary>
    private static readonly HashSet<string> OccupationCodes = ["364703007", "11295-3", "11341-5"];

    private static readonly XNamespace V3 = "urn:hl7-org:v3";

    /// <inheritdoc />
    public string Format => CdaFormat;

    /// <summary>
    /// Parse one eICR or RR for the intake seam: the patient, with the
    /// document's result organizers as their orders, reported by the
    /// document's custodian under its kind as the message type.
    /// </summary>
    public ParsedMessage Parse(string raw)
    {
        var document = Read(raw);
        return new ParsedMessage(CdaFormat, document.Kind, document.Custodian, [document.Patient]);
    }

    /// <summary>
    /// Read one eICR or RR. Throws <see cref="Hl7ParseException"/> when the
    /// text is not well-formed XML, not a ClinicalDocument, not an eICR or RR
    /// of a version read here, or has no id.
    /// </summary>
    public CdaDocument Read(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        XDocument xml;
        try
        {
            using var reader = XmlReader.Create(
                new StringReader(raw),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            xml = XDocument.Load(reader);
        }
        catch (XmlException ex)
        {
            throw new Hl7ParseException("The document is not well-formed XML.", ex);
        }

        var root = xml.Root;
        if (root is null || root.Name != V3 + "ClinicalDocument")
        {
            throw new Hl7ParseException("The document is not an HL7 CDA ClinicalDocument.");
        }

        var (kind, version) = KindOf(root);
        var documentId = FormatId(root.Element(V3 + "id"))
            ?? throw new Hl7ParseException($"The {kind} has no ClinicalDocument/id.");
        var isRr = kind == RrKind;

        return new CdaDocument(
            kind,
            version,
            documentId,
            FormatId(root.Element(V3 + "setId")),
            int.TryParse(Attribute(root.Element(V3 + "versionNumber"), "value"), out var number) ? number : null,
            Instant(Attribute(root.Element(V3 + "effectiveTime"), "value")),
            Custodian(root),
            Patient(root),
            Encounter(root),
            WithTemplate(root, ProblemObservation)
                .Select(problem => new CdaProblem(
                    Code(problem.Element(V3 + "value")),
                    Low(problem),
                    High(problem),
                    HasTemplate(problem, TriggerProblemObservation)))
                .ToList(),
            WithTemplate(root, MedicationActivity).Select(Medication).ToList(),
            WithTemplate(root, PregnancyObservation)
                .Select(pregnancy => new CdaPregnancy(
                    Code(pregnancy.Element(V3 + "value")),
                    Attribute(pregnancy, "negationInd") == "true",
                    Low(pregnancy),
                    High(pregnancy)))
                .FirstOrDefault(),
            WithTemplate(root, TravelHistory)
                .Select(travel => new CdaTravel(
                    TravelLocation(travel), Text(travel.Element(V3 + "text")), Low(travel), High(travel)))
                .ToList(),
            root.Descendants().Where(IsOccupation).Select(Occupation).ToList(),
            isRr ? FormatId(WithTemplate(root, EicrReference).FirstOrDefault()?.Element(V3 + "id")) : null,
            isRr ? WithTemplate(root, ReportableCondition).SelectMany(Conditions).ToList() : []);
    }

    /// <summary>
    /// An instance identifier as one string: its root, then a caret and its
    /// extension where it has one; null for a missing id or one with no root.
    /// </summary>
    public static string? FormatId(XElement? id)
    {
        var root = Attribute(id, "root");
        if (root is null)
        {
            return null;
        }

        var extension = Attribute(id, "extension");
        return extension is null ? root : $"{root}^{extension}";
    }

    private static (string Kind, string Version) KindOf(XElement root)
    {
        foreach (var template in root.Elements(V3 + "templateId"))
        {
            var templateRoot = Attribute(template, "root");
            var extension = Attribute(template, "extension") ?? string.Empty;

            if (templateRoot == EicrTemplate && EicrVersions.TryGetValue(extension, out var eicr))
            {
                return (EicrKind, eicr);
            }

            if (templateRoot == RrTemplate && RrVersions.TryGetValue(extension, out var rr))
            {
                return (RrKind, rr);
            }
        }

        throw new Hl7ParseException(
            "The document is not an eICR R1.1, R3.1 or R3.1.1 or an RR 1.1: no templateId names one.");
    }

    private static string? Custodian(XElement root)
    {
        var organization = root.Element(V3 + "custodian")?.Element(V3 + "assignedCustodian")
            ?.Element(V3 + "representedCustodianOrganization");
        return FormatId(organization?.Element(V3 + "id")) ?? Text(organization?.Element(V3 + "name"));
    }

    /// <summary>The patient: their name, birth and sex, and their first home address.</summary>
    private static ParsedPatient Patient(XElement root)
    {
        var role = root.Element(V3 + "recordTarget")?.Element(V3 + "patientRole");
        var patient = role?.Element(V3 + "patient");
        var names = patient?.Elements(V3 + "name").ToList() ?? [];
        var name = names.FirstOrDefault(n => Attribute(n, "use") == "L") ?? names.FirstOrDefault();
        var given = name?.Elements(V3 + "given").Select(Text).ToList() ?? [];
        var addresses = role?.Elements(V3 + "addr").ToList() ?? [];
        var address = addresses.FirstOrDefault(a => Attribute(a, "use") == "H") ?? addresses.FirstOrDefault();

        return new ParsedPatient(
            new CreatePersonRequest(
                LastName: Text(name?.Element(V3 + "family")),
                FirstName: given.ElementAtOrDefault(0),
                MiddleName: given.ElementAtOrDefault(1),
                BirthDate: Date(Attribute(patient?.Element(V3 + "birthTime"), "value")),
                BirthGender: Attribute(patient?.Element(V3 + "administrativeGenderCode"), "code")),
            Street: Text(address?.Element(V3 + "streetAddressLine")),
            City: Text(address?.Element(V3 + "city")),
            State: Text(address?.Element(V3 + "state")),
            Zip: Text(address?.Element(V3 + "postalCode")),
            Orders: WithTemplate(root, ResultOrganizer).Select(Order).ToList(),
            County: Text(address?.Element(V3 + "county")));
    }

    private static CdaEncounter? Encounter(XElement root)
    {
        var encounter = root.Element(V3 + "componentOf")?.Element(V3 + "encompassingEncounter");
        if (encounter is null)
        {
            return null;
        }

        var facility = encounter.Element(V3 + "location")?.Element(V3 + "healthCareFacility");
        return new CdaEncounter(
            FormatId(encounter.Element(V3 + "id")),
            Code(encounter.Element(V3 + "code")),
            Low(encounter),
            High(encounter),
            FormatId(facility?.Element(V3 + "id")),
            Text(facility?.Element(V3 + "serviceProviderOrganization")?.Element(V3 + "name")));
    }

    /// <summary>A result organizer as an order: its panel, its date, and its result observations.</summary>
    private static ParsedOrder Order(XElement organizer)
    {
        var code = organizer.Element(V3 + "code");
        var observedOn = Low(organizer);
        return new ParsedOrder(
            AccessionNo: FormatId(organizer.Element(V3 + "id")),
            TestCode: Attribute(code, "code"),
            TestName: Attribute(code, "displayName"),
            ObservedOn: observedOn,
            SpecimenCode: null,
            SpecimenText: null,
            CollectedOn: observedOn,
            Results: organizer.Elements(V3 + "component")
                .Select(component => component.Element(V3 + "observation"))
                .OfType<XElement>()
                .Where(observation => HasTemplate(observation, ResultObservation))
                .Select(Result)
                .ToList());
    }

    private static ParsedResult Result(XElement observation)
    {
        var code = observation.Element(V3 + "code");
        var value = observation.Element(V3 + "value");
        var coded = Attribute(value, "code") is not null;
        var range = observation.Element(V3 + "referenceRange")?.Element(V3 + "observationRange");
        var rangeValue = range?.Element(V3 + "value");

        return new ParsedResult(
            ObservationCode: Attribute(code, "code"),
            ObservationName: Attribute(code, "displayName"),
            ResultValue: Attribute(value, "value") ?? Attribute(value, "code") ?? Text(value),
            CodedResult: coded ? Attribute(value, "displayName") : null,
            Units: Attribute(value, "unit"),
            ReferenceRange: Text(range?.Element(V3 + "text")) ?? Range(rangeValue),
            AbnormalFlag: Attribute(observation.Element(V3 + "interpretationCode"), "code"),
            ResultStatus: Attribute(observation.Element(V3 + "statusCode"), "code"));
    }

    private static string? Range(XElement? value)
    {
        var low = Attribute(value?.Element(V3 + "low"), "value");
        var high = Attribute(value?.Element(V3 + "high"), "value");
        return low is null && high is null ? null : $"{low}-{high}";
    }

    private static CdaMedication Medication(XElement activity)
    {
        var dose = activity.Element(V3 + "doseQuantity");
        var doseValue = Attribute(dose, "value");
        var route = activity.Element(V3 + "routeCode");

        return new CdaMedication(
            Code(activity.Element(V3 + "consumable")?.Element(V3 + "manufacturedProduct")
                ?.Element(V3 + "manufacturedMaterial")?.Element(V3 + "code")),
            activity.Elements(V3 + "effectiveTime")
                .Select(time => Date(Attribute(time, "value")) ?? Date(Attribute(time.Element(V3 + "low"), "value")))
                .FirstOrDefault(date => date is not null),
            doseValue is null ? null : $"{doseValue} {Attribute(dose, "unit")}".TrimEnd(),
            Attribute(route, "displayName") ?? Attribute(route, "code"));
    }

    /// <summary>Where a travel entry went: its coded place, else the parts of its address.</summary>
    private static string? TravelLocation(XElement travel)
    {
        var place = travel.Element(V3 + "participant")?.Element(V3 + "participantRole");
        var code = place?.Element(V3 + "code");
        var address = place?.Element(V3 + "addr");
        var parts = address?.Elements().Select(Text).OfType<string>().ToList() ?? [];

        return Attribute(code, "displayName")
            ?? Attribute(code, "code")
            ?? (parts.Count == 0 ? null : string.Join(", ", parts));
    }

    private static bool IsOccupation(XElement element)
    {
        if (HasTemplate(element, PastOrPresentOccupation) || HasTemplate(element, UsualOccupation))
        {
            return true;
        }

        if (!HasTemplate(element, SocialHistoryObservation))
        {
            return false;
        }

        var code = element.Element(V3 + "code");
        return code is not null
            && code.DescendantsAndSelf()
                .Select(c => Attribute(c, "code"))
                .Any(c => c is not null && OccupationCodes.Contains(c));
    }

    private static CdaOccupation Occupation(XElement observation)
    {
        var value = observation.Element(V3 + "value");
        var coded = Attribute(value, "code") is not null;

        return new CdaOccupation(
            coded ? Code(value) : null,
            Text(observation.Element(V3 + "text")) ?? (coded ? null : Text(value)),
            Low(observation),
            High(observation));
    }

    /// <summary>
    /// One RR condition per responsible agency its reportability information
    /// names, or one with no agency where it names none.
    /// </summary>
    private static IEnumerable<RrCondition> Conditions(XElement observation)
    {
        var condition = Code(observation.Element(V3 + "value"));
        var organizers = WithTemplate(observation, ReportabilityInformation).ToList();
        if (organizers.Count == 0)
        {
            yield return new RrCondition(condition, null, null, null, null, null);
            yield break;
        }

        foreach (var organizer in organizers)
        {
            var agency = WithTemplate(organizer, ResponsibleAgency).FirstOrDefault()?.Element(V3 + "participantRole");
            var timeframe = WithTemplate(organizer, ReportingTimeframe).FirstOrDefault()?.Element(V3 + "value");
            var timeframeValue = Attribute(timeframe, "value");

            yield return new RrCondition(
                condition,
                Code(WithTemplate(organizer, DeterminationOfReportability).FirstOrDefault()?.Element(V3 + "value")),
                Code(organizer.Element(V3 + "code")),
                FormatId(agency?.Element(V3 + "id")),
                Text(agency?.Element(V3 + "playingEntity")?.Element(V3 + "name")),
                timeframeValue is null ? null : $"{timeframeValue} {Attribute(timeframe, "unit")}".TrimEnd());
        }
    }

    private static IEnumerable<XElement> WithTemplate(XElement scope, string templateRoot) =>
        scope.Descendants().Where(element => HasTemplate(element, templateRoot));

    private static bool HasTemplate(XElement element, string templateRoot) =>
        element.Elements(V3 + "templateId").Any(template => Attribute(template, "root") == templateRoot);

    private static CdaCode? Code(XElement? element)
    {
        var code = Attribute(element, "code");
        var displayName = Attribute(element, "displayName");
        return code is null && displayName is null
            ? null
            : new CdaCode(code, Attribute(element, "codeSystem"), displayName);
    }

    /// <summary>When an act began: its effectiveTime's low, else its effectiveTime's value.</summary>
    private static DateOnly? Low(XElement act)
    {
        var time = act.Element(V3 + "effectiveTime");
        return Date(Attribute(time?.Element(V3 + "low"), "value")) ?? Date(Attribute(time, "value"));
    }

    private static DateOnly? High(XElement act) =>
        Date(Attribute(act.Element(V3 + "effectiveTime")?.Element(V3 + "high"), "value"));

    /// <summary>
    /// The date an HL7 timestamp falls on, read to the precision it gives: a
    /// year alone is its first day, and a year and month the month's first.
    /// </summary>
    private static DateOnly? Date(string? timestamp)
    {
        if (timestamp is null)
        {
            return null;
        }

        var digits = new string(timestamp.TakeWhile(char.IsAsciiDigit).ToArray());
        var padded = digits.Length switch
        {
            >= 8 => digits[..8],
            6 => digits + "01",
            4 => digits + "0101",
            _ => null,
        };

        return padded is not null
            && DateOnly.TryParseExact(padded, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    /// <summary>
    /// The instant an HL7 timestamp names, to the precision it gives — at
    /// least a day, the missing time of day read as zeros and any fraction of
    /// a second dropped — at its offset, else at UTC; null where it is not one.
    /// </summary>
    private static DateTimeOffset? Instant(string? timestamp)
    {
        if (timestamp is null)
        {
            return null;
        }

        var digits = new string(timestamp.TakeWhile(char.IsAsciiDigit).ToArray());
        var zone = timestamp.IndexOfAny(['+', '-']);
        var offset = TimeSpan.Zero;
        if (zone >= 0)
        {
            var sign = timestamp[zone] == '-' ? -1 : 1;
            var zoneDigits = timestamp[(zone + 1)..];
            if (zoneDigits.Length != 4
                || !int.TryParse(zoneDigits[..2], NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
                || !int.TryParse(zoneDigits[2..], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
                || hours > 14
                || minutes > 59)
            {
                return null;
            }

            offset = sign * new TimeSpan(hours, minutes, 0);
        }

        return digits.Length is >= 8 and <= 14 && digits.Length % 2 == 0
            && DateTime.TryParseExact(
                digits.PadRight(14, '0'),
                "yyyyMMddHHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var time)
            ? new DateTimeOffset(time, offset)
            : null;
    }

    private static string? Attribute(XElement? element, string name) =>
        NullIfEmpty((string?)element?.Attribute(name));

    private static string? Text(XElement? element) => NullIfEmpty(element?.Value.Trim());

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
