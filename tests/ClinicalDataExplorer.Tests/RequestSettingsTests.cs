using ClinicalDataExplorer.Models;
using Xunit;
using static ClinicalDataExplorer.Tests.FhirCompatibilityTests;

namespace ClinicalDataExplorer.Tests;

public sealed class RequestSettingsTests
{
    [Fact]
    public async Task Retry_count_is_persisted_and_applies_to_subsequent_requests()
    {
        using var handler = new ScriptedHandler(
            _ => ScriptedHandler.Xml("", System.Net.HttpStatusCode.GatewayTimeout),
            _ => ScriptedHandler.Xml("", System.Net.HttpStatusCode.GatewayTimeout),
            _ => ScriptedHandler.Xml(Bundle("")));
        using var context = new FhirTestContext("https://fhir.example.test/r4/", handler);
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            context.Service.LoadPatientResourcePageAsync("p1", new PatientResourceLoad("Observation")));
        var settings = context.Settings.Current;
        settings.FhirTimeoutRetryCount = 1;
        await context.Settings.SaveAsync(settings);
        Assert.Equal(1, context.ReloadSettings().Current.FhirTimeoutRetryCount);
        await context.Service.LoadPatientResourcePageAsync("p1", new PatientResourceLoad("Observation"));
        Assert.Equal(3, handler.Calls);
    }

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

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Timeouts_stop_after_the_configured_number_of_retries(int retries)
    {
        using var handler = new StalledHandler();
        using var context = new FhirTestContext("https://fhir.example.test/r4/", handler);
        var settings = context.Settings.Current;
        Assert.Equal(0, settings.FhirTimeoutRetryCount);
        settings.FhirHttpTimeoutSeconds = 1;
        settings.FhirTimeoutRetryCount = retries;
        await context.Settings.SaveAsync(settings);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            context.Service.LoadPatientResourcePageAsync("p1", new PatientResourceLoad("Observation")));
        Assert.Equal(retries + 1, handler.Calls);
    }

    [Fact]
    public async Task A_timed_out_request_can_succeed_on_retry()
    {
        using var handler = new StalledHandler(succeedAfter: 1);
        using var context = new FhirTestContext("https://fhir.example.test/r4/", handler);
        var settings = context.Settings.Current;
        settings.FhirHttpTimeoutSeconds = 1;
        settings.FhirTimeoutRetryCount = 2;
        await context.Settings.SaveAsync(settings);
        await context.Service.LoadPatientResourcePageAsync("p1", new PatientResourceLoad("Observation"));
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Caller_deadline_does_not_trigger_retries()
    {
        using var handler = new StalledHandler();
        using var context = new FhirTestContext("https://fhir.example.test/r4/", handler);
        var settings = context.Settings.Current;
        settings.FhirTimeoutRetryCount = 2;
        await context.Settings.SaveAsync(settings);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            context.Service.LoadPatientResourcePageAsync("p1", new PatientResourceLoad("Observation"), deadline.Token));
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(408, 3)]
    [InlineData(504, 3)]
    [InlineData(500, 1)]
    [InlineData(401, 1)]
    public async Task Only_timeout_responses_are_retried(int status, int attempts)
    {
        using var handler = new ScriptedHandler(Enumerable.Range(0, attempts)
            .Select(_ => new Func<HttpRequestMessage, HttpResponseMessage>(request =>
                ScriptedHandler.Xml("", (System.Net.HttpStatusCode)status))).ToArray());
        using var context = new FhirTestContext("https://fhir.example.test/r4/", handler);
        var settings = context.Settings.Current;
        settings.FhirTimeoutRetryCount = 2;
        await context.Settings.SaveAsync(settings);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            context.Service.LoadPatientResourcePageAsync("p1", new PatientResourceLoad("Observation")));
        Assert.Equal((System.Net.HttpStatusCode)status, error.StatusCode);
        Assert.Equal(attempts, handler.Calls);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public async Task Invalid_retry_counts_are_rejected(int retries)
    {
        using var context = new FhirTestContext("https://fhir.example.test/r4/");
        var settings = context.Settings.Current;
        settings.FhirTimeoutRetryCount = retries;
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Settings.SaveAsync(settings));
        Assert.Equal(0, context.Settings.Current.FhirTimeoutRetryCount);
    }

    private sealed class StalledHandler(int succeedAfter = int.MaxValue) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            if (Calls <= succeedAfter) await Task.Delay(Timeout.Infinite, token);
            return ScriptedHandler.Xml(Bundle(""));
        }
    }
}
