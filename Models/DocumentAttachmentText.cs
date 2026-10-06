using System.Net.Mime;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ClinicalDataExplorer.Models;

public static class DocumentAttachmentText
{
    private static readonly XNamespace Fhir = "http://hl7.org/fhir";
    private static readonly XNamespace Presentation = "urn:clinical-data-explorer:presentation";

    public static void Annotate(XDocument document)
    {
        foreach (var attachment in document.Descendants(Fhir + "DocumentReference")
            .Elements(Fhir + "content").Elements(Fhir + "attachment"))
        {
            attachment.Elements(Presentation + "documentText").Remove();
            attachment.Elements(Presentation + "documentTextNotice").Remove();
            var data = (string?)attachment.Element(Fhir + "data")?.Attribute("value");
            if (string.IsNullOrWhiteSpace(data)) continue;
            try
            {
                var contentTypeValue = (string?)attachment.Element(Fhir + "contentType")?.Attribute("value");
                var contentType = string.IsNullOrWhiteSpace(contentTypeValue) ? null : new ContentType(contentTypeValue);
                var mediaType = contentType?.MediaType;
                if (mediaType is not null && !mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) &&
                    !mediaType.Equals("application/xml", StringComparison.OrdinalIgnoreCase) &&
                    !mediaType.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase) &&
                    !mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
                {
                    Notice(attachment, "This attachment is not a text document. Inline text preview is unavailable.");
                    continue;
                }
                // Bound decoded preview size without changing the original attachment.
                if (data.Length > 4 * 1024 * 1024)
                {
                    Notice(attachment, "This attachment is too large for an inline text preview.");
                    continue;
                }
                var bytes = Convert.FromBase64String(data);
                var encoding = string.IsNullOrWhiteSpace(contentType?.CharSet)
                    ? new UTF8Encoding(false, true)
                    : Encoding.GetEncoding(contentType.CharSet.Trim('"'), EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                var text = encoding.GetString(bytes).TrimStart('\uFEFF');
                if (text.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t') ||
                    text.StartsWith("%PDF-", StringComparison.Ordinal) || text.StartsWith(@"{\rtf", StringComparison.Ordinal))
                {
                    Notice(attachment, "This attachment could not be displayed as readable document text.");
                    continue;
                }
                XmlConvert.VerifyXmlChars(text);
                if (!string.IsNullOrWhiteSpace(text))
                    attachment.Add(new XElement(Presentation + "documentText", text.Replace("\r\n", "\n").Replace('\r', '\n')));
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or XmlException or NotSupportedException)
            {
                Notice(attachment, "This attachment could not be decoded as document text.");
            }
        }
    }

    private static void Notice(XElement attachment, string message) =>
        attachment.Add(new XElement(Presentation + "documentTextNotice", message));
}
