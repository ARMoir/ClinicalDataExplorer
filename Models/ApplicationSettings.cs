using System.ComponentModel.DataAnnotations;

namespace ClinicalDataExplorer.Models;

public sealed class ApplicationSettings
{
    [Required]
    [StringLength(120)]
    public string ProductName { get; set; } = "Clinical Data Explorer";

    [Required]
    [StringLength(120)]
    public string FacilityName { get; set; } = "Your Facility";

    [Required]
    [StringLength(2048)]
    public string FhirBaseUrl { get; set; } = "http://summittest:8080/";

    /// <summary>
    /// Current supported values: Windows or Disabled.
    /// The string setting intentionally leaves room for OIDC and Local later.
    /// </summary>
    [Required]
    [RegularExpression("^(Windows|Disabled)$", ErrorMessage = "Authentication mode must be Windows or Disabled.")]
    public string AuthenticationMode { get; set; } = "Windows";

    /// <summary>
    /// Optional NetBIOS or DNS domain restriction. Leave blank to accept any
    /// Windows identity authenticated by the host.
    /// </summary>
    [StringLength(120)]
    public string WindowsDomain { get; set; } = "";

    public bool AllUsersAreAdministrators { get; set; } = true;
    public string AdministratorUsers { get; set; } = "";

    public string? LogoPath { get; set; }

    [Required]
    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Use a 6-digit hex color such as #1F618D.")]
    public string PrimaryColor { get; set; } = "#1F618D";

    [Required]
    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Use a 6-digit hex color such as #17202A.")]
    public string SecondaryColor { get; set; } = "#17202A";

    public ApplicationSettings Clone() => new()
    {
        ProductName = ProductName,
        FacilityName = FacilityName,
        FhirBaseUrl = FhirBaseUrl,
        AuthenticationMode = AuthenticationMode,
        WindowsDomain = WindowsDomain,
        AllUsersAreAdministrators = AllUsersAreAdministrators,
        AdministratorUsers = AdministratorUsers,
        LogoPath = LogoPath,
        PrimaryColor = PrimaryColor,
        SecondaryColor = SecondaryColor
    };
}
