using ClinicalDataExplorer.Models;
using Xunit;
using static ClinicalDataExplorer.Tests.FhirCompatibilityTests;

namespace ClinicalDataExplorer.Tests;

public sealed class RequestSettingsTests
{
    [Fact]
    public async Task Saved_page_size_applies_to_new_searches_and_preserves_server_cursor()
    {
        using var handler = new ScriptedHandler(
            r => { Assert.Contains("_count=25", r.RequestUri!.Query); return ScriptedHandler.Xml(Bundle("", "Patient/p1/Observation?cursor=2&_count=7")); },
            r => { Assert.Equal("?cursor=2&_count=7", r.RequestUri!.Query); return ScriptedHandler.Xml(Bundle("")); },
            r => { Assert.Contains("_count=75", r.RequestUri!.Query); return ScriptedHandler.Xml(Bundle("")); });
        using var context = new FhirTestContext("https://fhir.example.test/r4/", handler);
        var settings = context.Settings.Current;
        Assert.Equal(30, settings.FhirHttpTimeoutSeconds);
        Assert.Equal(50, settings.FhirRequestPageSize);
        settings.FhirRequestPageSize = 25;
        await context.Settings.SaveAsync(settings);
        var source = new PatientResourceLoad("Observation");
        await context.Service.LoadPatientResourcePageAsync("p1", source);
        settings.FhirRequestPageSize = 75;
        await context.Settings.SaveAsync(settings);
        await context.Service.LoadPatientResourcePageAsync("p1", source);
        await context.Service.LoadPatientResourcePageAsync("p1", new PatientResourceLoad("Condition"));
    }

    [Fact]
    public async Task Saved_timeout_cancels_a_stalled_request()
    {
        using var handler = new StalledHandler();
        using var context = new FhirTestContext("https://fhir.example.test/r4/", handler);
        var settings = context.Settings.Current;
        settings.FhirHttpTimeoutSeconds = 1;
        await context.Settings.SaveAsync(settings);
        var pending = context.Service.LoadPatientResourcePageAsync("p1", new PatientResourceLoad("Observation"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(3601, 50)]
    [InlineData(30, 0)]
    [InlineData(30, 1001)]
    public async Task Invalid_values_are_rejected(int timeout, int count)
    {
        using var context = new FhirTestContext("https://fhir.example.test/r4/");
        var settings = context.Settings.Current;
        settings.FhirHttpTimeoutSeconds = timeout;
        settings.FhirRequestPageSize = count;
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Settings.SaveAsync(settings));
        Assert.Equal(30, context.Settings.Current.FhirHttpTimeoutSeconds);
        Assert.Equal(50, context.Settings.Current.FhirRequestPageSize);
    }

    private sealed class StalledHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            await Task.Delay(Timeout.Infinite, token);
            return ScriptedHandler.Xml(Bundle(""));
        }
    }
}
