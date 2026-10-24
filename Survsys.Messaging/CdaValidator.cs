using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace Survsys.Messaging;

/// <summary>
/// Validates an eCR document against the CDA R2 schema (with the SDTC
/// extensions) and against the HL7 schematron for its kind and version,
/// answering what each found as <see cref="CdaValidationFinding"/>s.
/// </summary>
/// <remarks>
/// The schema and schematrons are the HL7 files under <c>Cda/</c>, read from
/// beside the assembly, unchanged (see <c>Cda/README.md</c>). The schematrons
/// declare XSLT 2.0 but test with XPath 1.0 throughout apart from two
/// <c>matches()</c> calls, so each is compiled to XSLT 1.0 with the ISO
/// Schematron skeleton: in memory its query binding is read as <c>xslt1</c>
/// and <c>matches()</c> is bound to a .NET regular expression. Compiling one
/// takes a second or two, so each is compiled once, on first use, and kept.
/// Validation only reports: what to do about a finding is the caller's.
/// </remarks>
public sealed class CdaValidator
{
    /// <summary>The source of a finding from the CDA schema.</summary>
    public const string SchemaSource = "schema";

    /// <summary>The source of a finding from the HL7 schematron.</summary>
    public const string SchematronSource = "schematron";

    /// <summary>A finding the guide treats as making the document unusable.</summary>
    public const string Fatal = "fatal";

    /// <summary>A finding that breaks a SHALL of the guide or the schema.</summary>
    public const string Error = "error";

    /// <summary>A finding that breaks a SHOULD of the guide.</summary>
    public const string Warning = "warning";

    private const string RegexNamespace = "urn:survsys:regex";
    private const string R31Vocabulary = "CDAR2_IG_PHCASERPT_R2_D3_VOCABULARY.xml";
    private const string R311Vocabulary = "CDAR2_IG_PHCASERPT_R2_STU3.1.1_VOCABULARY.xml";

    private static readonly XNamespace Sch = "http://purl.oclc.org/dsdl/schematron";
    private static readonly XNamespace Svrl = "http://purl.oclc.org/dsdl/svrl";
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Cda");

    /// <summary>The schematron file for each kind and IG version <see cref="CdaParser"/> reads.</summary>
    private static readonly Dictionary<(string Kind, string Version), string> Schematrons = new()
    {
        [(CdaParser.EicrKind, "R1.1")] = "CDAR2_IG_PHCASERPT_R2_STU1.1_SCHEMATRON.sch",
        [(CdaParser.EicrKind, "R3.1")] = "CDAR2_IG_PHCASERPT_R2_D3_SCHEMATRON.sch",
        [(CdaParser.EicrKind, "R3.1.1")] = "CDAR2_IG_PHCASERPT_R2_STU3.1.1_SCHEMATRON.sch",
        [(CdaParser.RrKind, "1.1")] = "CDAR2_IG_PHCR_R2_RR_D1_2017DEC_SCHEMATRON.sch",
    };

    private static readonly Lazy<XmlSchemaSet> Schema = new(LoadSchema);
    private static readonly ConcurrentDictionary<string, Lazy<XslCompiledTransform>> Compiled = new();

    /// <summary>
    /// Validate one document of the given kind and IG version, as
    /// <see cref="CdaParser.Read"/> answered them. Throws
    /// <see cref="ArgumentException"/> for a kind and version with no
    /// schematron, and <see cref="XmlException"/> for text that is not XML.
    /// </summary>
    public IReadOnlyList<CdaValidationFinding> Validate(string raw, string kind, string igVersion)
    {
        ArgumentNullException.ThrowIfNull(raw);
        if (!Schematrons.TryGetValue((kind, igVersion), out var schematron))
        {
            throw new ArgumentException($"There is no schematron for {kind} {igVersion}.", nameof(igVersion));
        }

        var findings = new List<CdaValidationFinding>();
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            ValidationType = ValidationType.Schema,
            Schemas = Schema.Value,
        };
        settings.ValidationEventHandler += (_, e) =>
        {
            if (e.Severity == XmlSeverityType.Error)
            {
                findings.Add(new CdaValidationFinding(
                    SchemaSource, Error, e.Message, $"line {e.Exception.LineNumber}, position {e.Exception.LinePosition}"));
            }
        };

        using (var reader = XmlReader.Create(new StringReader(raw), settings))
        {
            while (reader.Read())
            {
            }
        }

        // Read again without the schema, which would otherwise add its default
        // attributes to what the schematron sees.
        XPathDocument document;
        using (var reader = XmlReader.Create(
            new StringReader(raw), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
        {
            document = new XPathDocument(reader);
        }

        var transform = Compiled.GetOrAdd(schematron, name => new Lazy<XslCompiledTransform>(() => Compile(name))).Value;
        var arguments = new XsltArgumentList();
        arguments.AddExtensionObject(RegexNamespace, new RegexFunctions());
        var report = new XDocument();
        using (var writer = report.CreateWriter())
        {
            transform.Transform(document, arguments, writer, new VocabularyResolver());
        }

        findings.AddRange(SchematronFindings(report));
        return findings;
    }

    /// <summary>
    /// The failed asserts and successful reports of an SVRL report. Each takes
    /// its severity from the role of the rule it fell under, or where the rule
    /// has none, as the RR 1.1 schematron's rules do, from its pattern: that
    /// schematron sorts its patterns into ones whose ids end "-errors" and ones
    /// whose ids end "-warnings".
    /// </summary>
    private static IEnumerable<CdaValidationFinding> SchematronFindings(XDocument report)
    {
        string? pattern = null;
        string? role = null;
        foreach (var element in report.Root?.Elements() ?? [])
        {
            if (element.Name == Svrl + "active-pattern")
            {
                pattern = (string?)element.Attribute("id");
            }
            else if (element.Name == Svrl + "fired-rule")
            {
                role = (string?)element.Attribute("role");
            }
            else if (element.Name == Svrl + "failed-assert" || element.Name == Svrl + "successful-report")
            {
                yield return new CdaValidationFinding(
                    SchematronSource,
                    Severity(role, pattern),
                    Regex.Replace(element.Element(Svrl + "text")?.Value ?? string.Empty, @"\s+", " ").Trim(),
                    (string?)element.Attribute("location"));
            }
        }
    }

    private static string Severity(string? role, string? pattern)
    {
        if (string.IsNullOrEmpty(role))
        {
            return pattern?.EndsWith("-warnings", StringComparison.Ordinal) == true ? Warning : Error;
        }

        // The guides' roles are fatal, error, warning and severe-warning
        // (spelt "servere-warning" once in R3.1.1).
        return role == Fatal ? Fatal : role.Contains(Warning, StringComparison.Ordinal) ? Warning : Error;
    }

    private static XmlSchemaSet LoadSchema()
    {
        var schemas = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
        schemas.Add(null, Path.Combine(Folder, "Schema", "infrastructure", "cda", "CDA_SDTC.xsd"));
        schemas.Compile();
        return schemas;
    }

    /// <summary>
    /// Compile one schematron to an XSLT 1.0 validator: the ISO skeleton's
    /// include, abstract-expansion and SVRL stages, each run over the last.
    /// Every stage, and the validator itself, is given the schematron's own
    /// address, so its <c>document()</c> calls find the vocabulary beside it.
    /// </summary>
    private static XslCompiledTransform Compile(string name)
    {
        var path = Path.Combine(Folder, "Schematron", name);
        var schematron = XDocument.Load(path);
        var root = schematron.Root!;
        root.SetAttributeValue("queryBinding", "xslt1");
        foreach (var test in root.Descendants().Attributes("test"))
        {
            test.Value = Regex.Replace(test.Value, @"(?<![\w:.-])matches\(", "regex:matches(");
        }

        root.AddFirst(new XElement(Sch + "ns", new XAttribute("prefix", "regex"), new XAttribute("uri", RegexNamespace)));

        var baseUri = new Uri(path).AbsoluteUri;
        var stages = new[] { "iso_dsdl_include.xsl", "iso_abstract_expand.xsl", "iso_svrl_for_xslt1.xsl" };
        var current = schematron;
        foreach (var stage in stages)
        {
            var transform = new XslCompiledTransform();
            transform.Load(Path.Combine(Folder, "Iso", stage), new XsltSettings(true, false), new XmlUrlResolver());
            var output = new XDocument();
            using (var reader = Reader(current, baseUri))
            using (var writer = output.CreateWriter())
            {
                transform.Transform(reader, null, writer, new XmlUrlResolver());
            }

            current = output;
        }

        var validator = new XslCompiledTransform();
        using (var reader = Reader(current, baseUri))
        {
            validator.Load(reader, new XsltSettings(true, false), new XmlUrlResolver());
        }

        return validator;
    }

    private static XmlReader Reader(XDocument document, string baseUri) =>
        XmlReader.Create(new StringReader(document.ToString()), null, baseUri);

    /// <summary>
    /// Resolves the schematrons' <c>document()</c> calls on disk, reading the
    /// R3.1 vocabulary, which is not kept, from the R3.1.1 vocabulary, which
    /// holds every value set the R3.1 schematron looks up.
    /// </summary>
    private sealed class VocabularyResolver : XmlUrlResolver
    {
        public override Uri ResolveUri(Uri? baseUri, string? relativeUri) =>
            relativeUri == R31Vocabulary
                ? new Uri(Path.Combine(Folder, "Schematron", R311Vocabulary))
                : base.ResolveUri(baseUri, relativeUri);
    }

    /// <summary>The XPath 2.0 <c>matches()</c> the R3.1.1 schematron tests ids with.</summary>
    private sealed class RegexFunctions
    {
        // Named as XPath names it, because the schematron calls it by that name.
        public bool matches(string input, string pattern) => Regex.IsMatch(input, pattern);
    }
}
