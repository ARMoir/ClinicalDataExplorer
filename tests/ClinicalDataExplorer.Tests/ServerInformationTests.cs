using System.Net;
using ClinicalDataExplorer.Models;
using Xunit;
using static ClinicalDataExplorer.Tests.FhirCompatibilityTests;

namespace ClinicalDataExplorer.Tests;

public sealed class ServerInformationTests
{
    private const string BaseUrl = "https://fhir.example.test/r4/";
    [Fact]
    public async Task Metadata_discovers_only_searchable_shared_categories_from_server_mode()
    {
        using var handler = new ScriptedHandler(request =>
        {
            Assert.Equal("/r4/metadata", request.RequestUri!.AbsolutePath);
            return ScriptedHandler.Xml("""
                <CapabilityStatement xmlns="http://hl7.org/fhir"><rest><mode value="server"/>
                <resource><type value="Patient"/><interaction><code value="search-type"/></interaction></resource>
                <resource><type value="Observation"/><interaction><code value="search-type"/></interaction></resource>
                <resource><type value="CodeSystem"/><interaction><code value="search-type"/></interaction></resource>
                <resource><type value="Organization"/><interaction><code value="search-type"/></interaction></resource>
                <resource><type value="StructureDefinition"/><interaction><code value="read"/></interaction></resource>
                </rest><rest><mode value="client"/><resource><type value="Questionnaire"/><interaction><code value="search-type"/></interaction></resource></rest></CapabilityStatement>
                """);
        });
        using var context = new FhirTestContext(BaseUrl, handler);
        var result = await context.Service.GetServerInformationAsync();
        Assert.Equal(new[] { "CodeSystem", "Organization" }, result.ReferenceTypes);
    }

    [Fact]
    public async Task Terminology_prefers_actual_server_capabilities()
    {
        using var handler = new ScriptedHandler(request =>
        {
            Assert.Contains("mode=terminology", request.RequestUri!.Query);
            return ScriptedHandler.Xml("<TerminologyCapabilities xmlns='http://hl7.org/fhir'><status value='active'/></TerminologyCapabilities>");
        });
        using var context = new FhirTestContext(BaseUrl, handler);
        var result = await context.Service.GetTerminologyInformationAsync();
        Assert.Equal("Server terminology capabilities", result.Source);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(404)]
    [InlineData(200)]
    public async Task Terminology_falls_back_to_published_statements_when_mode_is_unsupported(int status)
    {
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml("<CapabilityStatement xmlns='http://hl7.org/fhir'/>", (HttpStatusCode)status),
            request =>
            {
                Assert.Equal("/r4/TerminologyCapabilities", request.RequestUri!.AbsolutePath);
                return ScriptedHandler.Xml(Bundle("<entry><resource><TerminologyCapabilities><id value='t1'/></TerminologyCapabilities></resource></entry>", "TerminologyCapabilities?cursor=2"));
            });
        using var context = new FhirTestContext(BaseUrl, handler);
        var result = await context.Service.GetTerminologyInformationAsync();
        Assert.Equal("Published terminology statements", result.Source);
        Assert.NotNull(result.Next);
        Assert.Null(result.Total);
    }

    [Fact]
    public async Task Permission_failure_is_not_hidden_by_terminology_fallback()
    {
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml("<OperationOutcome xmlns='http://hl7.org/fhir'/>", HttpStatusCode.Forbidden));
        using var context = new FhirTestContext(BaseUrl, handler);
        await Assert.ThrowsAsync<HttpRequestException>(() => context.Service.GetTerminologyInformationAsync());
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Reference_pages_preserve_opaque_continuation_and_report_totals_when_present()
    {
        using var handler = new ScriptedHandler(request =>
        {
            Assert.Equal("?cursor=opaque%2B2", request.RequestUri!.Query);
            return ScriptedHandler.Xml(Bundle("<total value='12'/><entry><resource><ValueSet><id value='v11'/></ValueSet></resource></entry>"));
        });
        using var context = new FhirTestContext(BaseUrl, handler);
        var result = await context.Service.GetReferencePageAsync("ValueSet", new Uri(BaseUrl + "ValueSet?cursor=opaque%2B2"));
        Assert.Equal(12, result.Total);
        Assert.Equal(1, result.Count);
        Assert.Null(result.Next);
    }

    [Fact]
    public async Task Reference_browser_rejects_patient_categories_without_requesting_them()
    {
        using var handler = new ScriptedHandler();
        using var context = new FhirTestContext(BaseUrl, handler);
        await Assert.ThrowsAsync<ArgumentException>(() => context.Service.GetReferencePageAsync("Observation"));
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("<entry><resource><Patient><id value='p1'/></Patient></resource></entry>", null)]
    [InlineData("", "https://other.example/CodeSystem?cursor=2")]
    public async Task Reference_browser_rejects_wrong_records_and_external_pages(string entries, string? next)
    {
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml(Bundle(entries, next)));
        using var context = new FhirTestContext(BaseUrl, handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.GetReferencePageAsync("CodeSystem"));
    }

    [Fact]
    public void Reference_template_renders_readable_summary_and_safe_nested_details()
    {
        var html = PatientExplorerTests.Transform("ServerInformation", """
            <CapabilityStatement xmlns="http://hl7.org/fhir"><title value="Test server"/><software><name value="Example"/><version value="1.0"/></software><description value="&lt;script&gt;bad&lt;/script&gt;"/><rest><mode value="server"/><resource><type value="Patient"/><searchParam><name value="family"/></searchParam></resource></rest></CapabilityStatement>
            """);
        Assert.Contains("Test server", html);
        Assert.Contains("Software", html);
        Assert.Contains("Technical details", html);
        Assert.Contains("family", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>", html);
    }
}
