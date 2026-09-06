using System.Xml.Linq;
using ClinicalDataExplorer.Models;

namespace ClinicalDataExplorer.Services;

public sealed partial class FhirService
{
    public async Task<PatientResourceSection> GetReferencedRecordAsync(string reference, CancellationToken cancellationToken = default)
    {
        var baseUri = GetBaseUri();
        var uri = FhirReferenceLinks.Resolve(reference, baseUri);
        var path = uri.AbsolutePath[baseUri.AbsolutePath.Length..].Split('/');
        var document = ParseDocument(await GetXmlAsync(new Uri(uri.AbsoluteUri + "?_format=xml"), baseUri, cancellationToken));
        if (document.Root?.Name != Fhir + path[0] || Value(document.Root, "id") != path[1])
            throw new InvalidOperationException("The server did not return the referenced record.");
        await ResolvePractitionerReferencesAsync(document, cancellationToken);
        return new PatientResourceSection(path[0], [new XElement(document.Root)]);
    }
}
