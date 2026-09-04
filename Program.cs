using System.Net.Http.Headers;
using ClinicalDataExplorer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// The FHIR base URL is intentionally NOT configured here. It is read from the
// JSON-backed ApplicationSettingsService for each request, allowing it to be
// changed from the Settings page without rebuilding or restarting the app.
builder.Services.AddHttpClient("Fhir", client =>
{
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/fhir+xml"));
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddScoped<FhirService>();
builder.Services.AddSingleton<ApplicationSettingsService>();
builder.Services.AddSingleton<XsltService>();
builder.Services.AddSingleton<XmlDisplayService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<ClinicalDataExplorer.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
