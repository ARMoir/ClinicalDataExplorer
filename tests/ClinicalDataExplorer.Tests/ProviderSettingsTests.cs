using ClinicalDataExplorer.Models;
using ClinicalDataExplorer.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using static ClinicalDataExplorer.Tests.FhirCompatibilityTests;

namespace ClinicalDataExplorer.Tests;

public sealed class ProviderSettingsTests
{
    private const string BaseUrl = "https://fhir.example.test/r4/";

    [Fact]
    public async Task Default_skips_patient_provider_requests_and_setting_persists()
    {
        Assert.False(new ApplicationSettings().ShowProviderAssociations);
        Assert.False(System.Text.Json.JsonSerializer.Deserialize<ApplicationSettings>("{}")!.ShowProviderAssociations);
        using var handler = new ScriptedHandler();
        using var context = new FhirTestContext(BaseUrl, handler);
        var patient = new PatientSummary("p1", "Example", [], null, null, null, null, null);
        Assert.Same(patient, await context.Service.AddPatientPractitionersAsync(patient));
        Assert.Same(patient, await context.Service.AddRecentPatientPractitionersAsync(patient));
        Assert.Equal(0, handler.Calls);
        foreach (var enabled in new[] { true, false })
        {
            var settings = context.Settings.Current;
            settings.ShowProviderAssociations = enabled;
            await context.Settings.SaveAsync(settings);
            Assert.Equal(enabled, context.Settings.Current.ShowProviderAssociations);
            Assert.Equal(enabled, context.ReloadSettings().Current.ShowProviderAssociations);
        }
    }

    [Fact]
    public async Task Default_skips_diagnostic_provider_scan_and_reference_fetches()
    {
        const string report = "<entry><resource><DiagnosticReport><id value='r1'/><performer><reference value='Practitioner/p1'/></performer></DiagnosticReport></resource></entry>";
        using var handler = new ScriptedHandler(
            _ => ScriptedHandler.Xml(Bundle(report)),
            r => { Assert.DoesNotContain("_include:iterate", r.RequestUri!.Query); return ScriptedHandler.Xml(Bundle(report)); });
        using var context = new FhirTestContext(BaseUrl, handler);
        await foreach (var item in context.Service.StreamRecentReportsAsync(true))
        {
            Assert.Empty(item.Providers);
            Assert.Null(item.ProviderError);
        }
        await context.Service.GetDiagnosticReportByIdAsync("r1");
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Provider_list_is_only_rendered_when_enabled(bool enabled)
    {
        using var context = new FhirTestContext(BaseUrl, showProviderAssociations: enabled);
        var services = new ServiceCollection().AddLogging().AddSingleton(context.Settings);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<ProviderList>(ParameterView.Empty)).ToHtmlString());
        Assert.Equal(enabled, html.Contains("Associated providers:"));
        Assert.Equal(enabled, html.Contains("No practitioner references returned."));
    }
}
