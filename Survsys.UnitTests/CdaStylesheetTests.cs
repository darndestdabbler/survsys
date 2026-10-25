using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The HL7 CDA stylesheet run over each eICR sample: it renders to HTML
/// carrying the document's title, and with its JavaScript off.
/// </summary>
public class CdaStylesheetTests
{
    [Theory]
    [InlineData(CdaParserTests.EicrR11)]
    [InlineData(CdaParserTests.EicrR31)]
    [InlineData(CdaParserTests.EicrR311)]
    public void RenderShowsTheEicrWithoutScript(string fixture)
    {
        var html = new CdaStylesheet().Render(CdaParserTests.ReadFixture(fixture));

        Assert.Contains("<html", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Initial Public Health Case Report", html);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RenderRefusesTextThatIsNotXml()
    {
        Assert.Throws<System.Xml.XmlException>(() => new CdaStylesheet().Render("not xml"));
    }
}
