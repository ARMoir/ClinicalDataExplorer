using System.Net;
using System.Text;
using ClinicalDataExplorer.Models;
using ClinicalDataExplorer.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace ClinicalDataExplorer.Tests;

internal sealed class FhirTestContext : IDisposable, IHttpClientFactory
{
    private readonly DirectoryInfo root = Directory.CreateTempSubdirectory("ClinicalDataExplorer-tests-");
    public HttpClient Client { get; }
    public FhirService Service { get; }
    public ApplicationSettingsService Settings { get; }

    public FhirTestContext(string baseUrl, HttpMessageHandler? handler = null)
    {
        baseUrl = baseUrl.TrimEnd('/') + "/";
        Client = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
        Client.DefaultRequestHeaders.Accept.ParseAdd("application/fhir+xml");
        Client.DefaultRequestHeaders.TryAddWithoutValidation("Prefer", "handling=strict");
        var settings = new ApplicationSettingsService(new TestEnvironment(root.FullName));
        Settings = settings;
        // Only the disposable test directory is written; never load work settings.
        settings.SaveAsync(new ApplicationSettings { FhirBaseUrl = baseUrl }).GetAwaiter().GetResult();
        Service = new FhirService(this, settings);
    }

    public HttpClient CreateClient(string name)
    {
        Assert.Equal("Fhir", name);
        return Client;
    }

    public void Dispose()
    {
        Client.Dispose();
        root.Delete(recursive: true);
    }

    private sealed class TestEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "ClinicalDataExplorer.Tests";
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = root;
        public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}

internal sealed class ScriptedHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] steps) : HttpMessageHandler
{
    private int next;
    public int Calls => next;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Contains(request.Headers.Accept, value => value.MediaType == "application/fhir+xml");
        Assert.Contains("handling=strict", request.Headers.GetValues("Prefer"));
        Assert.True(next < steps.Length, "Unexpected additional FHIR request.");
        return Task.FromResult(steps[next++](request));
    }

    public static HttpResponseMessage Xml(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/fhir+xml") };
}
