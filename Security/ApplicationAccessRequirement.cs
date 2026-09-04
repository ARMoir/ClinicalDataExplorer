using Microsoft.AspNetCore.Authorization;

namespace ClinicalDataExplorer.Security;

/// <summary>
/// Application-level access requirement. Authentication is currently either
/// Windows/Active Directory or disabled for development. Future OIDC/local
/// providers can plug into the same authorization layer.
/// </summary>
public sealed class ApplicationAccessRequirement : IAuthorizationRequirement
{
}
