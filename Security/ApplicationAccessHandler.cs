using ClinicalDataExplorer.Services;
using Microsoft.AspNetCore.Authorization;

namespace ClinicalDataExplorer.Security;

public sealed class ApplicationAccessHandler : AuthorizationHandler<ApplicationAccessRequirement>
{
    private readonly ApplicationSettingsService _settingsService;

    public ApplicationAccessHandler(ApplicationSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ApplicationAccessRequirement requirement)
    {
        var settings = _settingsService.Current;
        var mode = settings.AuthenticationMode?.Trim();

        // Development/emergency mode. Unknown modes intentionally fail closed.
        if (string.Equals(mode, "Disabled", StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (!string.Equals(mode, "Windows", StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        var identity = context.User.Identity;
        if (identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(identity.Name))
            return Task.CompletedTask;

        if (!IsAllowedDomain(identity.Name, settings.WindowsDomain))
            return Task.CompletedTask;

        context.Succeed(requirement);
        return Task.CompletedTask;
    }

    private static bool IsAllowedDomain(string identityName, string? configuredDomain)
    {
        if (string.IsNullOrWhiteSpace(configuredDomain))
            return true;

        var domain = configuredDomain.Trim().TrimEnd('\\');

        // Normal Windows identity form: DOMAIN\\username
        if (identityName.StartsWith(domain + "\\", StringComparison.OrdinalIgnoreCase))
            return true;

        // Also tolerate UPN-style identities when the configured value is a DNS domain.
        if (identityName.EndsWith("@" + domain, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
