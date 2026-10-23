using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace Survsys.Messaging;

/// <summary>
/// Renders an eCR document to HTML through the HL7 CDA stylesheet
/// (<c>Cda/Stylesheet/CDA.xsl</c>, XSLT 1.0), as a reviewer reads it.
/// </summary>
/// <remarks>
/// The stylesheet is compiled once and kept. It is run with its JavaScript
/// off, and its <c>document()</c> calls are resolved only against the files
/// beside it, so a document cannot make the rendering read anything else. The
/// HTML is still the document's own text, so it is to be shown sandboxed.
/// </remarks>
public sealed class CdaStylesheet
{
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Cda", "Stylesheet");
    private static readonly Lazy<XslCompiledTransform> Transform = new(Load);

    /// <summary>
    /// The document as HTML. Throws <see cref="XmlException"/> for text that
    /// is not XML.
    /// </summary>
    public string Render(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        XPathDocument document;
        using (var reader = XmlReader.Create(
            new StringReader(raw), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
        {
            document = new XPathDocument(reader);
        }

        var arguments = new XsltArgumentList();
        arguments.AddParam("useJavascript", string.Empty, "false");
        var html = new StringWriter();
        using (var writer = XmlWriter.Create(html, Transform.Value.OutputSettings))
        {
            Transform.Value.Transform(document, arguments, writer, new StylesheetFolderResolver());
        }

        return html.ToString();
    }

    private static XslCompiledTransform Load()
    {
        var transform = new XslCompiledTransform();
        transform.Load(Path.Combine(Folder, "CDA.xsl"), new XsltSettings(true, false), new StylesheetFolderResolver());
        return transform;
    }

    /// <summary>Resolves a file beside the stylesheet, and refuses any other address.</summary>
    private sealed class StylesheetFolderResolver : XmlUrlResolver
    {
        public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
        {
            if (!absoluteUri.IsFile
                || !string.Equals(
                    Path.GetDirectoryName(absoluteUri.LocalPath), Folder.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                throw new XmlException($"The CDA stylesheet may not read {absoluteUri}.");
            }

            return base.GetEntity(absoluteUri, role, ofObjectToReturn);
        }
    }
}
