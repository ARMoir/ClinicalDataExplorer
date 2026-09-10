using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.WebUtilities;

namespace ClinicalDataExplorer.Services;

public sealed class UserActivityService(AuditStore store, AuthenticationStateProvider authentication, ApplicationSettingsService settings)
{
    public string Session { get; } = Guid.NewGuid().ToString("N");
    public static bool IsAdministrator(ClaimsPrincipal user, Models.ApplicationSettings settings) =>
        settings.AllUsersAreAdministrators || (user.Identity?.IsAuthenticated == true &&
        settings.AdministratorUsers.Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(user.Identity.Name ?? "", StringComparer.OrdinalIgnoreCase));
    public async Task<bool> IsAdministratorAsync() => IsAdministrator((await authentication.GetAuthenticationStateAsync()).User, settings.Current);
    public async Task RequireAdministratorAsync()
    {
        if (await IsAdministratorAsync()) return;
        await RecordAsync("AdminAccess", "Settings/Audit", "Denied");
        throw new UnauthorizedAccessException("Administrator access is required.");
    }
    public async Task RecordAsync(string action, string target, string outcome = "Observed", string details = "")
    {
        var user = (await authentication.GetAuthenticationStateAsync()).User;
        store.Append(user.Identity?.IsAuthenticated == true ? user.Identity.Name ?? "Unnamed authenticated user" : "Anonymous", Session, action, target, outcome, details);
    }
    // Search values and paging tokens are deliberately excluded; resource IDs may occur in paths.
    public static string SafeTarget(string url)
    {
        var uri = new Uri(new Uri("https://local.invalid/"), url);
        var keys = QueryHelpers.ParseQuery(uri.Query).Keys.Order(StringComparer.Ordinal);
        return uri.AbsolutePath + (uri.Query.Length == 0 ? "" : "?" + string.Join("&", keys));
    }
    // Page/print context can preserve validated resource IDs without retaining
    // report identifiers, search values, tokens, or arbitrary external URLs.
    public static string SafePageTarget(string url)
    {
        var uri = new Uri(new Uri("https://local.invalid/"), url);
        var query = QueryHelpers.ParseQuery(uri.Query);
        string? key = uri.AbsolutePath == "/reports/view" ? "reportId" : uri.AbsolutePath == "/record" ? "reference" : null;
        if (key is null || !query.TryGetValue(key, out var values) || values.Count != 1) return SafeTarget(url);
        var value = values[0] ?? "";
        var pattern = key == "reportId" ? @"\A[A-Za-z0-9\-.]{1,64}\z" : @"\A[A-Z][A-Za-z]+/[A-Za-z0-9\-.]{1,64}\z";
        return System.Text.RegularExpressions.Regex.IsMatch(value, pattern)
            ? SafeTarget(url) + "; " + (key == "reportId" ? "DiagnosticReport/" : "") + value : SafeTarget(url);
    }
    public async Task<IReadOnlyList<AuditEntry>> ReadAsync(string user, string action, long before)
    {
        await RequireAdministratorAsync();
        await RecordAsync("AuditReview", "/audit", "Attempted");
        try
        {
            var entries = store.Read(user, action, before);
            await RecordAsync("AuditReview", "/audit", "Succeeded", "Events returned: " + entries.Count);
            return entries;
        }
        catch (Exception ex)
        {
            await RecordAsync("AuditReview", "/audit", "Failed", ex.GetType().Name);
            throw;
        }
    }
}
