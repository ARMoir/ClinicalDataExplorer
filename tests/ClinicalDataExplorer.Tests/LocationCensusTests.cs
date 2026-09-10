using ClinicalDataExplorer.Models;
using Xunit;
using static ClinicalDataExplorer.Tests.FhirCompatibilityTests;

namespace ClinicalDataExplorer.Tests;

public sealed class LocationCensusTests
{
    private const string BaseUrl = "https://fhir.example.test/r4/";
    private static string Entry(string resource) => $"<entry><resource>{resource}</resource></entry>";
    private static string Location(string id) => Entry($"<Location><id value='{id}'/><name value='Ward {id}'/></Location>");
    private static string Encounter(string id, string status, string locations = "", string period = "") =>
        Entry($"<Encounter><id value='{id}'/><status value='{status}'/>{period}{locations}<subject><reference value='Patient/p1'/></subject></Encounter>");
    private static string Assignment(string id, string status = "active", string period = "") =>
        $"<location><location><reference value='{id}'/></location><status value='{status}'/>{period}</location>";
    private const string Patient = "<entry><resource><Patient><id value='p1'/><name><text value='Test Patient'/></name></Patient></resource></entry>";

    [Fact]
    public async Task Census_reads_all_pages_and_deduplicates_encounters_and_assignments()
    {
        var encounter = Encounter("e1", "in-progress", Assignment("Location/a") + Assignment(BaseUrl + "Location/a"));
        using var handler = new ScriptedHandler(
            request => { Assert.Equal("/r4/Location", request.RequestUri!.AbsolutePath); return ScriptedHandler.Xml(Bundle(Location("a"), "Location?cursor=2")); },
            _ => ScriptedHandler.Xml(Bundle(Location("b"))),
            request => { Assert.Contains("status=arrived,triaged,in-progress,onleave", Uri.UnescapeDataString(request.RequestUri!.Query)); return ScriptedHandler.Xml(Bundle(encounter, "Encounter?cursor=2")); },
            _ => ScriptedHandler.Xml(Bundle(encounter + Patient)));
        using var context = new FhirTestContext(BaseUrl, handler);
        var groups = await context.Service.GetLocationCensusAsync();
        Assert.Equal(2, groups.Count);
        Assert.Equal("Test Patient", Assert.Single(groups[0].Encounters).Patient!.DisplayName);
        Assert.Empty(groups[1].Encounters);
        Assert.Equal(4, handler.Calls);
    }

    [Fact]
    public async Task Census_excludes_completed_future_and_historical_assignments()
    {
        var past = "<period><end value='2000-01-01T00:00:00Z'/></period>";
        var future = "<period><start value='2999-01-01T00:00:00Z'/></period>";
        var assignments = Assignment("Location/a", "completed") + Assignment("Location/a", "active", past) +
            Assignment("Location/a", "planned") + Assignment("Location/a", "active", future) + Assignment("Location/b");
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml(Bundle(Location("a") + Location("b"))),
            _ => ScriptedHandler.Xml(Bundle(Patient + Encounter("current", "in-progress", assignments) +
                Encounter("ended", "in-progress", period: past) + Encounter("future", "arrived", period: future) +
                Encounter("done", "finished") + Encounter("cancelled", "cancelled") + Encounter("planned", "planned"))));
        using var context = new FhirTestContext(BaseUrl, handler);
        var groups = await context.Service.GetLocationCensusAsync();
        Assert.Empty(groups.Single(g => g.Key == "Location/a").Encounters);
        Assert.Equal("current", Assert.Single(groups.Single(g => g.Key == "Location/b").Encounters).Encounter.Id);
    }

    [Fact]
    public async Task Census_preserves_unassigned_encounters_and_reads_missing_patient_once()
    {
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml(Bundle("")),
            _ => ScriptedHandler.Xml(Bundle(Encounter("e1", "triaged") + Encounter("e2", "onleave"))),
            request => { Assert.Equal("/r4/Patient/p1", request.RequestUri!.AbsolutePath); return ScriptedHandler.Xml("<Patient xmlns='http://hl7.org/fhir'><id value='p1'/><name><text value='Test Patient'/></name></Patient>"); });
        using var context = new FhirTestContext(BaseUrl, handler);
        var group = Assert.Single(await context.Service.GetLocationCensusAsync());
        Assert.Equal("No current location recorded", group.Name);
        Assert.Equal(2, group.Encounters.Count);
        Assert.All(group.Encounters, row => Assert.Equal("Test Patient", row.Patient!.DisplayName));
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task Census_rejects_external_paging_instead_of_showing_partial_results()
    {
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml(Bundle(Location("a"), "https://other.example/Location?cursor=2")));
        using var context = new FhirTestContext(BaseUrl, handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.GetLocationCensusAsync());
        Assert.Equal(1, handler.Calls);
    }
}
