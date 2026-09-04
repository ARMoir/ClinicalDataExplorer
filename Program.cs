using System.Net.Http.Headers;
using ClinicalDataExplorer.Security;
using ClinicalDataExplorer.Services;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// Windows Authentication uses the current Windows/Active Directory identity
// supplied by IIS, IIS Express, or Kestrel through Negotiate (Kerberos/NTLM).
builder.Services
    .AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();

builder.Services.AddCascadingAuthenticationState();

// Authorization remains application-controlled. The custom requirement reads
// the JSON-backed AuthenticationMode and optional WindowsDomain at runtime.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .AddRequirements(new ApplicationAccessRequirement())
        .Build();
});

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
builder.Services.AddSingleton<IAuthorizationHandler, ApplicationAccessHandler>();
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
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<ClinicalDataExplorer.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
