using System.Collections.Concurrent;
using System.Text;
using System.Xml;
using System.Xml.Xsl;
using ClinicalDataExplorer.Models;

namespace ClinicalDataExplorer.Services;

public sealed class XsltService(IWebHostEnvironment environment)
{
    private static readonly HashSet<string> AllowedTemplates = new(StringComparer.OrdinalIgnoreCase)
    {
        "PatientList", "PatientDetails", "EncounterList", "EncounterDetails",
        "ObservationList", "DiagnosticReportBundle"
    };

    private readonly ConcurrentDictionary<string, CachedTransform> _cache = new(StringComparer.OrdinalIgnoreCase);

    public string Transform(
        string templateName,
        string xml,
        ApplicationSettings settings,
        IReadOnlyDictionary<string, string>? parameters = null)
    {
        if (!AllowedTemplates.Contains(templateName))
            throw new ArgumentException("Unknown XSLT report template.", nameof(templateName));

        var path = Path.Combine(environment.ContentRootPath, "XSLT", templateName + ".xslt");
        var modified = File.GetLastWriteTimeUtc(path);
        var cached = _cache.AddOrUpdate(
            templateName,
            _ => Load(path, modified),
            (_, current) => current.ModifiedUtc == modified ? current : Load(path, modified));

        using var inputText = new StringReader(xml);
        using var input = XmlReader.Create(inputText, SecureReaderSettings());
        var arguments = CreateArguments(settings, parameters);
        var builder = new StringBuilder();
        using var outputText = new StringWriter(builder);
        var outputSettings = cached.Transform.OutputSettings?.Clone() ?? new XmlWriterSettings();
        outputSettings.OmitXmlDeclaration = true;
        outputSettings.ConformanceLevel = ConformanceLevel.Fragment;
        using var output = XmlWriter.Create(outputText, outputSettings);
        cached.Transform.Transform(input, arguments, output);
        output.Flush();
        return builder.ToString();
    }

    public string TransformDiagnosticReportBundle(string xml, ApplicationSettings settings) =>
        Transform("DiagnosticReportBundle", xml, settings);

    private static CachedTransform Load(string path, DateTime modified)
    {
        var transform = new XslCompiledTransform();
        using var stylesheet = XmlReader.Create(path, SecureReaderSettings());
        transform.Load(stylesheet, new XsltSettings(enableDocumentFunction: false, enableScript: false), null);
        return new CachedTransform(modified, transform);
    }

    private static XmlReaderSettings SecureReaderSettings() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null
    };

    private static XsltArgumentList CreateArguments(
        ApplicationSettings settings,
        IReadOnlyDictionary<string, string>? parameters)
    {
        var arguments = new XsltArgumentList();
        arguments.AddParam("facilityName", string.Empty, settings.FacilityName ?? string.Empty);
        arguments.AddParam("logoPath", string.Empty, settings.LogoPath ?? string.Empty);
        arguments.AddParam("primaryColor", string.Empty, settings.PrimaryColor ?? "#1F618D");
        arguments.AddParam("secondaryColor", string.Empty, settings.SecondaryColor ?? "#17202A");
        if (parameters is not null)
        {
            foreach (var parameter in parameters)
                arguments.AddParam(parameter.Key, string.Empty, parameter.Value);
        }
        return arguments;
    }

    private sealed record CachedTransform(DateTime ModifiedUtc, XslCompiledTransform Transform);
}
