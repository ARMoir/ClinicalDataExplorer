using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ClinicalDataExplorer.Services;

public sealed class XmlDisplayService
{
    public string PrettyPrintAndHighlight(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return string.Empty;

        string formatted;

        try
        {
            var document = XDocument.Parse(xml, LoadOptions.None);

            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\n",
                NewLineHandling = NewLineHandling.Replace,
                OmitXmlDeclaration = document.Declaration is null
            };

            var builder = new StringBuilder();
            using (var writer = XmlWriter.Create(builder, settings))
            {
                document.Save(writer);
            }

            formatted = builder.ToString();
        }
        catch
        {
            // If the endpoint ever returns malformed XML, still show it safely.
            formatted = xml;
        }

        return HighlightXml(formatted);
    }

    private static string HighlightXml(string xml)
    {
        var output = new StringBuilder(xml.Length * 2);
        var index = 0;

        while (index < xml.Length)
        {
            if (xml.AsSpan(index).StartsWith("<!--"))
            {
                var end = xml.IndexOf("-->", index, StringComparison.Ordinal);
                end = end >= 0 ? end + 3 : xml.Length;
                AppendSpan(output, "xml-comment", xml[index..end]);
                index = end;
                continue;
            }

            if (xml.AsSpan(index).StartsWith("<![CDATA["))
            {
                var end = xml.IndexOf("]]>", index, StringComparison.Ordinal);
                end = end >= 0 ? end + 3 : xml.Length;
                AppendSpan(output, "xml-cdata", xml[index..end]);
                index = end;
                continue;
            }

            if (xml.AsSpan(index).StartsWith("<?"))
            {
                var end = xml.IndexOf("?>", index, StringComparison.Ordinal);
                end = end >= 0 ? end + 2 : xml.Length;
                AppendSpan(output, "xml-declaration", xml[index..end]);
                index = end;
                continue;
            }

            if (xml[index] == '<')
            {
                var end = FindTagEnd(xml, index);
                var token = xml[index..end];
                HighlightTag(output, token);
                index = end;
                continue;
            }

            var nextTag = xml.IndexOf('<', index);
            if (nextTag < 0)
                nextTag = xml.Length;

            var text = xml[index..nextTag];
            if (string.IsNullOrWhiteSpace(text))
            {
                output.Append(WebUtility.HtmlEncode(text));
            }
            else
            {
                AppendSpan(output, "xml-text", text);
            }

            index = nextTag;
        }

        return output.ToString();
    }

    private static int FindTagEnd(string xml, int start)
    {
        var quote = '\0';

        for (var i = start + 1; i < xml.Length; i++)
        {
            var ch = xml[i];

            if (quote != '\0')
            {
                if (ch == quote)
                    quote = '\0';
                continue;
            }

            if (ch is '\'' or '"')
            {
                quote = ch;
                continue;
            }

            if (ch == '>')
                return i + 1;
        }

        return xml.Length;
    }

    private static void HighlightTag(StringBuilder output, string tag)
    {
        var i = 0;

        AppendSpan(output, "xml-punctuation", "<");
        i++;

        if (i < tag.Length && tag[i] == '/')
        {
            AppendSpan(output, "xml-punctuation", "/");
            i++;
        }
        else if (i < tag.Length && tag[i] == '!')
        {
            AppendSpan(output, "xml-punctuation", "!");
            i++;
        }

        var nameStart = i;
        while (i < tag.Length && !char.IsWhiteSpace(tag[i]) && tag[i] != '/' && tag[i] != '>')
            i++;

        if (i > nameStart)
            AppendSpan(output, "xml-tag", tag[nameStart..i]);

        while (i < tag.Length)
        {
            if (tag[i] == '>')
            {
                AppendSpan(output, "xml-punctuation", ">");
                i++;
                continue;
            }

            if (tag[i] == '/' && i + 1 < tag.Length && tag[i + 1] == '>')
            {
                AppendSpan(output, "xml-punctuation", "/>");
                i += 2;
                continue;
            }

            if (char.IsWhiteSpace(tag[i]))
            {
                output.Append(WebUtility.HtmlEncode(tag[i].ToString()));
                i++;
                continue;
            }

            var attributeStart = i;
            while (i < tag.Length &&
                   !char.IsWhiteSpace(tag[i]) &&
                   tag[i] != '=' &&
                   tag[i] != '>' &&
                   !(tag[i] == '/' && i + 1 < tag.Length && tag[i + 1] == '>'))
            {
                i++;
            }

            if (i > attributeStart)
                AppendSpan(output, "xml-attribute", tag[attributeStart..i]);

            while (i < tag.Length && char.IsWhiteSpace(tag[i]))
            {
                output.Append(WebUtility.HtmlEncode(tag[i].ToString()));
                i++;
            }

            if (i < tag.Length && tag[i] == '=')
            {
                AppendSpan(output, "xml-punctuation", "=");
                i++;

                while (i < tag.Length && char.IsWhiteSpace(tag[i]))
                {
                    output.Append(WebUtility.HtmlEncode(tag[i].ToString()));
                    i++;
                }

                if (i < tag.Length && tag[i] is '\'' or '"')
                {
                    var quote = tag[i];
                    AppendSpan(output, "xml-punctuation", quote.ToString());
                    i++;

                    var valueStart = i;
                    while (i < tag.Length && tag[i] != quote)
                        i++;

                    if (i > valueStart)
                        AppendSpan(output, "xml-attribute-value", tag[valueStart..i]);

                    if (i < tag.Length)
                    {
                        AppendSpan(output, "xml-punctuation", quote.ToString());
                        i++;
                    }
                }
            }
        }
    }

    private static void AppendSpan(StringBuilder output, string cssClass, string value)
    {
        output.Append("<span class=\"")
            .Append(cssClass)
            .Append("\">")
            .Append(WebUtility.HtmlEncode(value))
            .Append("</span>");
    }
}
