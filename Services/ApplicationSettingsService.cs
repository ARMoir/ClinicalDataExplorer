using System.Text.Json;
using ClinicalDataExplorer.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace ClinicalDataExplorer.Services;

public sealed class ApplicationSettingsService
{
    private const long MaxLogoBytes = 5 * 1024 * 1024;
    private readonly string _settingsPath;
    private readonly string _uploadsPath;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private ApplicationSettings _current;

    public ApplicationSettingsService(IWebHostEnvironment environment)
    {
        var dataDirectory = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDirectory);

        _uploadsPath = Path.Combine(environment.WebRootPath, "uploads");
        Directory.CreateDirectory(_uploadsPath);

        _settingsPath = Path.Combine(dataDirectory, "application-settings.json");
        _current = LoadFromDisk();
    }

    public event Action? Changed;

    public ApplicationSettings Current => _current.Clone();

    public async Task SaveAsync(ApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalized = new ApplicationSettings
        {
            ProductName = string.IsNullOrWhiteSpace(settings.ProductName) ? "Clinical Data Explorer" : settings.ProductName.Trim(),
            FacilityName = string.IsNullOrWhiteSpace(settings.FacilityName) ? "Your Facility" : settings.FacilityName.Trim(),
            FhirBaseUrl = NormalizeFhirBaseUrl(settings.FhirBaseUrl),
            LogoPath = settings.LogoPath,
            PrimaryColor = NormalizeColor(settings.PrimaryColor, "#1F618D"),
            SecondaryColor = NormalizeColor(settings.SecondaryColor, "#17202A")
        };

        await _saveLock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(normalized, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_settingsPath, json);
            _current = normalized;
            CleanupOldLogos(normalized.LogoPath);
        }
        finally
        {
            _saveLock.Release();
        }

        Changed?.Invoke();
    }

    public async Task<string> SaveLogoAsync(IBrowserFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var extension = file.ContentType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            _ => throw new InvalidOperationException("Logo must be a PNG, JPG, or WebP image.")
        };

        var targetName = $"facility-logo-{DateTime.UtcNow:yyyyMMddHHmmssfff}{extension}";
        var targetPath = Path.Combine(_uploadsPath, targetName);

        await using var input = file.OpenReadStream(MaxLogoBytes);
        await using var output = File.Create(targetPath);
        await input.CopyToAsync(output);

        return $"/uploads/{targetName}";
    }

    private void CleanupOldLogos(string? keepLogoPath)
    {
        var keepName = string.IsNullOrWhiteSpace(keepLogoPath)
            ? null
            : Path.GetFileName(keepLogoPath);

        foreach (var path in Directory.EnumerateFiles(_uploadsPath, "facility-logo-*"))
        {
            if (!string.Equals(Path.GetFileName(path), keepName, StringComparison.OrdinalIgnoreCase))
                File.Delete(path);
        }
    }

    private ApplicationSettings LoadFromDisk()
    {
        if (!File.Exists(_settingsPath))
            return new ApplicationSettings();

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<ApplicationSettings>(json);
            return settings ?? new ApplicationSettings();
        }
        catch
        {
            // A bad settings file should not prevent the viewer from starting.
            return new ApplicationSettings();
        }
    }

    private static string NormalizeFhirBaseUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("FHIR base URL is required.");

        var trimmed = value.Trim();

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "FHIR base URL must be an absolute HTTP or HTTPS URL, for example http://summittest:8080/.");
        }

        return trimmed.TrimEnd('/') + "/";
    }

    private static string NormalizeColor(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var color = value.Trim();
        if (color.Length == 7 && color[0] == '#' && color.Skip(1).All(Uri.IsHexDigit))
            return color.ToUpperInvariant();

        return fallback;
    }
}
