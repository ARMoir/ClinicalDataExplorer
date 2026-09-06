using System.Globalization;
using System.Xml.Linq;

namespace ClinicalDataExplorer.Models;

public static class ResourceChronology
{
    private static readonly XNamespace Fhir = "http://hl7.org/fhir";
    // Prefer the clinical event date; use administrative dates and last update
    // only when no event date is available. Undated records retain their order.
    private static readonly string[] Paths = [
        "effectiveDateTime", "effectiveInstant", "effectivePeriod/start", "effectivePeriod/end",
        "performedDateTime", "performedPeriod/start", "occurrenceDateTime", "occurrencePeriod/start",
        "period/start", "period/end", "start", "started", "servicePeriod/start", "collectedDateTime",
        "collection/collectedDateTime", "collection/collectedPeriod/start", "onsetDateTime", "onsetPeriod/start",
        "authoredOn", "date", "dateTime", "recordedDate", "issued", "created", "authored",
        "whenHandedOver", "whenPrepared", "sent", "received", "receivedTime", "executionPeriod/start",
        "timingDateTime", "timingPeriod/start", "billablePeriod/start", "meta/lastUpdated"
    ];

    public static DateTimeOffset? Date(XElement resource)
    {
        foreach (var path in Paths)
        {
            XElement? element = resource;
            foreach (var part in path.Split('/')) element = element?.Element(Fhir + part);
            var value = (string?)element?.Attribute("value");
            if (value?.Length == 4) value += "-01-01";
            else if (value?.Length == 7) value += "-01";
            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)) return date;
        }
        return null;
    }
}
