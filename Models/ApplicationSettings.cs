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
        LogoPath = LogoPath,
        PrimaryColor = PrimaryColor,
        SecondaryColor = SecondaryColor
    };
}
