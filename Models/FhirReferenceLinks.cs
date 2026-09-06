using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace ClinicalDataExplorer.Models;

public static class FhirReferenceLinks
{
    private static readonly XNamespace Fhir = "http://hl7.org/fhir";
    private static readonly XNamespace Presentation = "urn:clinical-data-explorer:presentation";

    public static Uri Resolve(string reference, Uri baseUri)
    {
        if (!Uri.TryCreate(baseUri, reference, out var uri) || uri.Scheme != baseUri.Scheme ||
            uri.Host != baseUri.Host || uri.Port != baseUri.Port ||
            !uri.AbsolutePath.StartsWith(baseUri.AbsolutePath, StringComparison.Ordinal) ||
            uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.UserInfo.Length > 0)
            throw new InvalidOperationException("This reference is not a record on the configured FHIR server.");
        var path = uri.AbsolutePath[baseUri.AbsolutePath.Length..];
        if (!Regex.IsMatch(path, @"\A[A-Z][A-Za-z]+/[A-Za-z0-9.-]{1,64}(?:/_history/[A-Za-z0-9.-]{1,64})?\z"))
            throw new InvalidOperationException("This reference does not identify a specific FHIR record.");
        return uri;
    }

    public static void Annotate(XDocument document, Uri baseUri, bool localTargets = true)
    {
        // Discard server-supplied presentation URLs before generating trusted links.
        foreach (var attribute in document.Descendants().Attributes().Where(a => a.Name == Presentation + "href" || a.Name == Presentation + "anchor").ToList()) attribute.Remove();
        var resources = document.Descendants().Where(e => e.Parent?.Name == Fhir + "resource" || e.Parent?.Name == Fhir + "contained").ToList();
        var fullUrls = new Dictionary<string, XElement>(StringComparer.Ordinal);
        for (var i = 0; i < resources.Count; i++)
        {
            var resource = resources[i];
            var owner = resource.Ancestors().FirstOrDefault(e => e.Parent?.Name == Fhir + "resource");
            static string Identity(XElement element) => element.Name.LocalName + "/" +
                ((string?)element.Element(Fhir + "id")?.Attribute("value") ?? element.ToString(SaveOptions.DisableFormatting));
            var identity = (owner is null ? "" : Identity(owner) + "/contained/") + Identity(resource);
            resource.SetAttributeValue(Presentation + "anchor", "fhir-record-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))));
            var fullUrl = (string?)resource.Parent?.Parent?.Element(Fhir + "fullUrl")?.Attribute("value");
            if (!string.IsNullOrEmpty(fullUrl)) fullUrls[fullUrl] = resource;
        }
        foreach (var reference in document.Descendants(Fhir + "reference"))
        {
            var value = (string?)reference.Attribute("value") ?? "";
            XElement? target = null;
            if (value.StartsWith('#'))
            {
                var owner = reference.Ancestors().FirstOrDefault(e => e.Parent?.Name == Fhir + "resource");
                target = owner?.Elements(Fhir + "contained").Elements().FirstOrDefault(e => (string?)e.Element(Fhir + "id")?.Attribute("value") == value[1..]);
            }
            else fullUrls.TryGetValue(value, out target);
            if (target is not null && localTargets)
                reference.SetAttributeValue(Presentation + "href", "#" + (string?)target.Attribute(Presentation + "anchor"));
            else
            {
                try
                {
                    if (target is not null && value.StartsWith("urn:", StringComparison.Ordinal))
                        value = target.Name.LocalName + "/" + (string?)target.Element(Fhir + "id")?.Attribute("value");
                    var uri = Resolve(value, baseUri);
                    reference.SetAttributeValue(Presentation + "href", "/record?reference=" + Uri.EscapeDataString(uri.AbsoluteUri));
                }
                catch (InvalidOperationException) { /* Non-resolvable references remain readable text. */ }
            }
        }
    }
}
