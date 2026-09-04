using System.Text;
using System.Xml;
using System.Xml.Xsl;
using ClinicalDataExplorer.Models;

namespace ClinicalDataExplorer.Services;

public sealed class XsltService
{
    private readonly XslCompiledTransform _diagnosticReportTransform = new();

    public XsltService(IWebHostEnvironment environment)
    {
        var path = Path.Combine(
            environment.ContentRootPath,
            "XSLT",
            "DiagnosticReportBundle.xslt");

        var xsltSettings = new XsltSettings(
            enableDocumentFunction: false,
            enableScript: false);

        var stylesheetReaderSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        using var stylesheetReader = XmlReader.Create(path, stylesheetReaderSettings);
        _diagnosticReportTransform.Load(stylesheetReader, xsltSettings, null);
    }

    public string TransformDiagnosticReportBundle(string xml, ApplicationSettings settings)
    {
        var readerSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        using var inputText = new StringReader(xml);
        using var input = XmlReader.Create(inputText, readerSettings);

        var arguments = new XsltArgumentList();
        arguments.AddParam("facilityName", string.Empty, settings.FacilityName ?? string.Empty);
        arguments.AddParam("logoPath", string.Empty, settings.LogoPath ?? string.Empty);
        arguments.AddParam("primaryColor", string.Empty, settings.PrimaryColor ?? "#1F618D");
        arguments.AddParam("secondaryColor", string.Empty, settings.SecondaryColor ?? "#17202A");

        var builder = new StringBuilder();
        using var outputText = new StringWriter(builder);

        var outputSettings = _diagnosticReportTransform.OutputSettings.Clone();
        outputSettings.OmitXmlDeclaration = true;
        outputSettings.ConformanceLevel = ConformanceLevel.Fragment;

        using var output = XmlWriter.Create(outputText, outputSettings);
        _diagnosticReportTransform.Transform(input, arguments, output);

        output.Flush();
        return builder.ToString();
    }
}
