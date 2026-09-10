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
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // Redirects must not bypass FhirService's configured-server boundary.
    AllowAutoRedirect = false
});

builder.Services.AddScoped<FhirService>();
builder.Services.AddSingleton<AuditStore>();
builder.Services.AddScoped<UserActivityService>();
builder.Services.AddScoped<PatientListService>();
builder.Services.AddSingleton<ApplicationSettingsService>();
builder.Services.AddSingleton<IAuthorizationHandler, ApplicationAccessHandler>();
builder.Services.AddSingleton<XsltService>();
builder.Services.AddSingleton<XmlDisplayService>();

var app = builder.Build();
// Initialize durable audit storage before accepting requests.
_ = app.Services.GetRequiredService<AuditStore>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    // Never retain patient pages in ordinary HTTP caches.
    context.Response.Headers.CacheControl = "no-store";
    await next(context);
});
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/_blazor") || context.Request.Path.StartsWithSegments("/_framework"))
    {
        await next(context);
        return;
    }
    var audit = context.RequestServices.GetRequiredService<AuditStore>();
    var user = context.User.Identity?.IsAuthenticated == true ? context.User.Identity.Name ?? "Unnamed authenticated user" : "Anonymous";
    var target = UserActivityService.SafeTarget(context.Request.Path + context.Request.QueryString);
    var source = "Remote IP: " + context.Connection.RemoteIpAddress;
    audit.Append(user, context.TraceIdentifier, "HttpAccess", target, "Attempted", source);
    try
    {
        await next(context);
        audit.Append(user, context.TraceIdentifier, "HttpAccess", target, context.Response.StatusCode is 401 or 403 ? "Denied" : context.Response.StatusCode >= 400 ? "Failed" : "Succeeded", "HTTP " + context.Response.StatusCode);
    }
    catch (Exception ex)
    {
        audit.Append(user, context.TraceIdentifier, "HttpAccess", target, "Failed", ex.GetType().Name);
        throw;
    }
});
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<ClinicalDataExplorer.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
