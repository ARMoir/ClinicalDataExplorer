using System.Text;
using System.Xml.Linq;
using ClinicalDataExplorer.Models;
using Xunit;

namespace ClinicalDataExplorer.Tests;

public sealed class DocumentAttachmentTextTests
{
    private static readonly XNamespace Fhir = "http://hl7.org/fhir";
    private static readonly XNamespace P = "urn:clinical-data-explorer:presentation";

    private static XElement Attachment(string? contentType, string? data) => new(Fhir + "content",
        new XElement(Fhir + "attachment",
            contentType is null ? null : new XElement(Fhir + "contentType", new XAttribute("value", contentType)),
            new XElement(Fhir + "title", new XAttribute("value", "Clinical note")),
            data is null ? null : new XElement(Fhir + "data", new XAttribute("value", data))));

    private static XDocument Bundle(params XElement[] contents) => new(new XElement(Fhir + "Bundle",
        new XElement(Fhir + "entry", new XElement(Fhir + "resource",
            new XElement(Fhir + "DocumentReference", new XElement(Fhir + "id", new XAttribute("value", "d1")), contents)))));

    [Theory]
    [InlineData(null)]
    [InlineData("text/plain")]
    [InlineData("text/plain; charset=utf-8")]
    [InlineData("text/html")]
    public void Base64_text_is_decoded_and_rendered_as_escaped_text(string? contentType)
    {
        const string text = "Clinical note: café\r\n<script>alert('test')</script>\r\nNext line";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        var document = Bundle(Attachment(contentType, encoded));
        DocumentAttachmentText.Annotate(document);
        Assert.Equal(text.Replace("\r\n", "\n"), document.Descendants(P + "documentText").Single().Value);
        Assert.Equal(encoded, (string?)document.Descendants(Fhir + "data").Single().Attribute("value"));
        var html = PatientExplorerTests.Transform("PatientResources", document.ToString());
        Assert.Contains("View document text", html);
        Assert.Contains("Clinical note: café", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("attachment-text", html);
        // Repeated annotation does not create duplicate previews.
        DocumentAttachmentText.Annotate(document);
        Assert.Single(document.Descendants(P + "documentText"));
    }

    [Fact]
    public void Declared_charset_and_whitespace_in_base64_are_supported()
    {
        var encoded = Convert.ToBase64String(Encoding.Latin1.GetBytes("Résumé"));
        var document = Bundle(Attachment("text/plain; charset=iso-8859-1", encoded.Insert(4, "\n ")));
        DocumentAttachmentText.Annotate(document);
        Assert.Equal("Résumé", document.Descendants(P + "documentText").Single().Value);
    }

    [Theory]
    [InlineData("text/plain", "not valid base64!")]
    [InlineData("application/pdf", "JVBERi0xLjc=")]
    [InlineData(null, "JVBERi0xLjc=")]
    [InlineData("text/plain", "AAECAw==")]
    [InlineData("text/plain", "/w==")]
    [InlineData("text/plain; charset=unknown-charset", "VGVzdA==")]
    public void Unreadable_attachments_do_not_break_other_document_text(string? contentType, string data)
    {
        var document = Bundle(Attachment(contentType, data), Attachment("text/plain", "UmVhZGFibGU="));
        DocumentAttachmentText.Annotate(document);
        Assert.Equal("Readable", document.Descendants(P + "documentText").Single().Value);
        Assert.Single(document.Descendants(P + "documentTextNotice"));
        var html = PatientExplorerTests.Transform("PatientResources", document.ToString());
        Assert.Contains("Readable", html);
        Assert.Contains("document-attachment-notice", html);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ICA=")]
    public void Missing_or_empty_text_does_not_create_an_empty_preview(string? data)
    {
        var document = Bundle(Attachment("text/plain", data));
        DocumentAttachmentText.Annotate(document);
        Assert.Empty(document.Descendants(P + "documentText"));
        Assert.DoesNotContain("View document text", PatientExplorerTests.Transform("PatientResources", document.ToString()));
    }
}
